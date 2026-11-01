Imports System
Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.Data.Sqlite

Public Class FrmMenuAutonome
    Inherits Form

    <System.Runtime.InteropServices.DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer,
                                                   ByRef value As Integer, size As Integer) As Integer
    End Function

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If Environment.OSVersion.Version.Build >= 22000 Then
            ' DWMWA_CAPTION_COLOR (Windows 11) : vert d'eau #A7E8D5.
            Dim captionColor As Integer = &HD5E8A7
            DwmSetWindowAttribute(Handle, 35, captionColor, 4)
            ' DWMWA_TEXT_COLOR : noir.
            Dim textColor As Integer = 0
            DwmSetWindowAttribute(Handle, 36, textColor, 4)
        End If
    End Sub


    Private infoWindow As Form
    Private infoText As TextBox
    Private ReadOnly menuBar As New MenuStrip()
    Private ReadOnly settingsPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "Theme_" & GeneratedConfig.SafeName & ".txt")
    Private titleColor As Color = Color.FromArgb(127, 255, 127)
    Private barColor As Color = Color.FromArgb(62, 130, 137)
    Private bodyColor As Color = Color.FromArgb(255, 128, 0)
    Private ReadOnly clock As New Timer With {.Interval = 1000}
    Private ReadOnly customTitle As New Panel With {.Dock = DockStyle.Top, .Height = 34}
    Private ReadOnly customTitleText As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True, .Padding = New Padding(10, 0, 0, 0)}
    Private slideshowTitleMode As Boolean
    Private previousTitleBounds As Rectangle
    Private previousTitleMinimum As Size
    Private ReadOnly databasePath As String = Path.Combine(AppContext.BaseDirectory, "CMenu.db")

    Public Sub New()
        Text = "Menu--" & GeneratedConfig.FormName
        StartPosition = FormStartPosition.CenterScreen
        ' Hauteur de la fenêtre : 4 cm, calculée selon la résolution DPI Windows.
        Dim height4cm As Integer
        Using screenGraphics = Graphics.FromHwnd(IntPtr.Zero)
            height4cm = CInt(Math.Round(screenGraphics.DpiY * 4.0 / 2.54))
        End Using
        Size = New Size(1050, height4cm)
        MinimumSize = New Size(180, height4cm)
        Font = New Font("Segoe UI", 10)
        FormBorderStyle = FormBorderStyle.None
        BackColor = bodyColor
        BuildCustomTitleBar()
        LoadColors()

        menuBar.Dock = DockStyle.Top
        menuBar.AutoSize = True
        menuBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
        menuBar.BackColor = Color.FromArgb(226, 234, 240)
        menuBar.Padding = New Padding(10, 6, 10, 6)
        Controls.Add(menuBar)
        Controls.Add(nextEventLabel)
        Controls.Add(appointmentLabel)
        Controls.Add(customTitle)
        Controls.SetChildIndex(customTitle, Controls.Count - 1)
        ApplyColors()
        KeyPreview = True
        AddHandler KeyDown, Sub(sender, e)
            If e.KeyCode = Keys.F5 Then
                e.Handled = True
                e.SuppressKeyPress = True
                RefreshNextEvent()
                RefreshAppointmentsDisplay()
            End If
        End Sub
        MainMenuStrip = menuBar

        AddHandler Resize, Sub(sender, e)
            UpdateRoundedCorners()
            PositionNextEvent()
            PositionAppointments()
            PositionSlideshow()
        End Sub
        AddHandler clock.Tick, Sub(sender, e) RefreshClock()
        AddHandler Shown, Sub(sender, e)
            UpdateRoundedCorners()
            RefreshClock()
            LoadFromDatabase()
            StartSlideshow(True)
            RefreshNextEvent()
            RefreshAppointmentsDisplay()
            clock.Start()
        End Sub
        AddHandler FormClosed, Sub(sender, e)
            clock.Stop()
            slideshowTimer.Stop()
        End Sub
    End Sub

    Private Sub UpdateRoundedCorners()
        If ClientSize.Width <= 24 OrElse ClientSize.Height <= 24 Then Return
        If WindowState = FormWindowState.Maximized Then
            Dim previous = Region
            Region = Nothing
            If previous IsNot Nothing Then previous.Dispose()
            Return
        End If
        Const radius As Integer = 14
        Dim diameter = radius * 2
        Using shape As New Drawing2D.GraphicsPath()
            Dim bounds = New Rectangle(0, 0, Width - 1, Height - 1)
            shape.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90)
            shape.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90)
            shape.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90)
            shape.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90)
            shape.CloseFigure()
            Dim previous = Region
            Region = New Region(shape)
            If previous IsNot Nothing Then previous.Dispose()
        End Using
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = &H84 Then
            MyBase.WndProc(m)
            If WindowState <> FormWindowState.Normal Then Return
            Dim packed = m.LParam.ToInt64()
            Dim point = PointToClient(New Point(CInt(CShort(packed And &HFFFFL)), CInt(CShort((packed >> 16) And &HFFFFL))))
            Const edge As Integer = 8
            Dim leftEdge = point.X >= 0 AndAlso point.X < edge
            Dim rightEdge = point.X < ClientSize.Width AndAlso point.X >= ClientSize.Width - edge
            Dim topEdge = point.Y >= 0 AndAlso point.Y < edge
            Dim bottomEdge = point.Y < ClientSize.Height AndAlso point.Y >= ClientSize.Height - edge
            Dim hit As Integer = 0
            If topEdge AndAlso leftEdge Then
                hit = 13
            ElseIf topEdge AndAlso rightEdge Then
                hit = 14
            ElseIf bottomEdge AndAlso leftEdge Then
                hit = 16
            ElseIf bottomEdge AndAlso rightEdge Then
                hit = 17
            ElseIf leftEdge Then
                hit = 10
            ElseIf rightEdge Then
                hit = 11
            ElseIf topEdge Then
                hit = 12
            ElseIf bottomEdge Then
                hit = 15
            End If
            If hit <> 0 Then m.Result = New IntPtr(hit)
            Return
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub BuildCustomTitleBar()
        customTitle.BackColor = titleColor
        Dim buttons As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .Width = 192, .Height = 34,
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .Margin = Padding.Empty, .Padding = Padding.Empty
        }
        Dim minimize As New Button With {.Text = "—", .Width = 48, .Height = 34, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        Dim small As New Button With {.Text = "8×8", .Width = 48, .Height = 34, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        Dim maximize As New Button With {.Text = "□", .Width = 48, .Height = 34, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        Dim closeButton As New Button With {.Text = "×", .Width = 48, .Height = 34, .FlatStyle = FlatStyle.Flat, .TabStop = False}
        For Each btn In {minimize, small, maximize, closeButton}
            btn.FlatAppearance.BorderSize = 0
            btn.Margin = Padding.Empty
            buttons.Controls.Add(btn)
        Next
        AddHandler minimize.Click, Sub(sender, e) WindowState = FormWindowState.Minimized
        AddHandler small.Click, Sub(sender, e) ToggleTitleSlideshowSize()
        AddHandler maximize.Click, Sub(sender, e)
            WindowState = If(WindowState = FormWindowState.Maximized, FormWindowState.Normal, FormWindowState.Maximized)
        End Sub
        AddHandler closeButton.Click, Sub(sender, e) Close()
        AddHandler customTitle.MouseDown, AddressOf DragTitle
        AddHandler customTitleText.MouseDown, AddressOf DragTitle
        AddHandler customTitleText.DoubleClick, Sub(sender, e) maximize.PerformClick()
        customTitle.Controls.Add(customTitleText)
        customTitle.Controls.Add(buttons)
    End Sub

    Private Sub DragTitle(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        If WindowState = FormWindowState.Maximized Then WindowState = FormWindowState.Normal
        NativeReleaseCapture()
        NativeSendMessage(Handle, &HA1, New IntPtr(2), IntPtr.Zero)
    End Sub

    <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="ReleaseCapture")>
    Private Shared Function NativeReleaseCapture() As Boolean
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="SendMessageW")>
    Private Shared Function NativeSendMessage(hwnd As IntPtr, msg As Integer, wp As IntPtr, lp As IntPtr) As IntPtr
    End Function

    Private Sub ToggleTitleSlideshowSize()
        If Not slideshowTitleMode Then
            If WindowState <> FormWindowState.Normal Then WindowState = FormWindowState.Normal
            previousTitleBounds = Bounds
            previousTitleMinimum = MinimumSize
            Dim work = Screen.FromControl(Me).WorkingArea
            Dim dpiX As Single
            Dim dpiY As Single
            Using graphics = CreateGraphics()
                dpiX = graphics.DpiX
                dpiY = graphics.DpiY
            End Using
            MinimumSize = Size.Empty
            Bounds = New Rectangle(work.Left, work.Top,
                Math.Min(work.Width, CInt(Math.Round(8.0 * dpiX / 2.54))),
                Math.Min(work.Height, CInt(Math.Round(8.0 * dpiY / 2.54))))
            slideshowTitleMode = True
        Else
            slideshowTitleMode = False
            Bounds = previousTitleBounds
            MinimumSize = previousTitleMinimum
        End If
    End Sub

    Private Sub RefreshClock()
        Dim nowValue = DateTime.Now
        Text = "Menu--" & GeneratedConfig.FormName & " — Nous sommes le " &
            nowValue.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")) &
            " à " & nowValue.ToString("HH:mm:ss")
        customTitleText.Text = Text
    End Sub

    Private Sub LoadFromDatabase()
        menuBar.Items.Clear()
        If Not File.Exists(databasePath) Then
            ShowProblem("Base SQLite introuvable : " & databasePath &
                        Environment.NewLine & "Copiez CMenu.db à côté de l’exécutable.")
            Return
        End If
        Try
            Dim records As New List(Of MenuRow)()
            Dim connectionString = New SqliteConnectionStringBuilder With {
                .DataSource = databasePath, .Mode = SqliteOpenMode.ReadOnly
            }.ToString()
            Using cn As New SqliteConnection(connectionString)
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText =
                        "SELECT Frm_Menu, NumMenu, NumSousMenu, Menu, SousMenu, Chemin, Action, Info01, Info02 " &
                        "FROM Menus WHERE TRIM(COALESCE(Menu,'')) <> '' " &
                        "AND (Frm_Menu = $form COLLATE NOCASE OR (TRIM(NumMenu) = '99' AND LOWER(TRIM(Menu)) = 'exemple')) " &
                        "ORDER BY CAST(NumMenu AS INTEGER), Menu COLLATE NOCASE, " &
                        "CAST(NumSousMenu AS INTEGER), SousMenu COLLATE NOCASE"
                    cmd.Parameters.AddWithValue("$form", GeneratedConfig.FormName)
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            records.Add(New MenuRow With {
                                .FormName = Value(reader, 0),
                                .MenuNumber = Value(reader, 1),
                                .SubNumber = Value(reader, 2),
                                .MenuText = Value(reader, 3),
                                .SubText = Value(reader, 4),
                                .Path = Value(reader, 5),
                                .ActionName = Value(reader, 6),
                                .Info01 = Value(reader, 7),
                                .Info02 = Value(reader, 8)
                            })
                        End While
                    End Using
                End Using
            End Using

            For Each group In records.GroupBy(
                Function(x) x.MenuNumber & ChrW(31) & x.MenuText,
                StringComparer.OrdinalIgnoreCase)
                Dim first = group.First()
                Dim top As New ToolStripMenuItem(first.MenuText.Trim()) With {
                    .AutoSize = True,
                    .ToolTipText = first.FormName
                }
                For Each record In group
                    If String.IsNullOrWhiteSpace(record.SubText) Then
                        If Not String.IsNullOrWhiteSpace(record.Path) OrElse IsStarterApplis(record.MenuText, record.ActionName) Then
                            Dim destination = record.Path
                            Dim actionName = record.ActionName
                            Dim info1 = record.Info01
                            Dim info2 = record.Info02
                            AddHandler top.Click, Sub(sender, e)
                                ShowInfo(info1, info2)
                                OpenPath(destination, actionName, first.MenuText)
                            End Sub
                        End If
                    Else
                        Dim child As New ToolStripMenuItem(record.SubText.Trim()) With {
                            .Tag = record
                        }
                        AddHandler child.Click, Sub(sender, e)
                            Dim clicked = DirectCast(sender, ToolStripMenuItem)
                            If String.Equals(first.MenuText.Trim(), "Exemple", StringComparison.OrdinalIgnoreCase) AndAlso first.MenuNumber.Trim() = "99" Then
                                ShowExampleDetails(DirectCast(clicked.Tag, MenuRow))
                            ElseIf String.Equals(clicked.Text.Trim(), "NomDeFichier", StringComparison.OrdinalIgnoreCase) Then
                                ShowCreateFileDialog()
                            ElseIf String.Equals(clicked.Text.Trim(), "NomDeDossier", StringComparison.OrdinalIgnoreCase) Then
                                ShowCreateFolderDialog()
                            ElseIf String.Equals(clicked.Text.Trim(), "Rendez-vous", StringComparison.OrdinalIgnoreCase) OrElse
                               String.Equals(clicked.Text.Trim(), "Rendez vous", StringComparison.OrdinalIgnoreCase) Then
                                Using dialog As New FrmRendezVous()
                                    dialog.ShowDialog(Me)
                                End Using
                            ElseIf String.Equals(clicked.Text.Trim(), "Anniversaire", StringComparison.OrdinalIgnoreCase) Then
                                Using dialog As New FrmAnniversaire()
                                    dialog.ShowDialog(Me)
                                End Using

                            ElseIf String.Equals(first.MenuText.Trim(), "Exemple", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(clicked.Text.Trim(), "Sauvegarde", StringComparison.OrdinalIgnoreCase) Then
                                SaveMenuDatabase(sender, e)
                            ElseIf String.Equals(first.MenuText.Trim(), "Exemple", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(clicked.Text.Trim(), "Restauration", StringComparison.OrdinalIgnoreCase) Then
                                RestoreMenuDatabase(sender, e)
                            ElseIf String.Equals(first.MenuText.Trim(), "BDD", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(clicked.Text.Trim(), "Started", StringComparison.OrdinalIgnoreCase) Then
                                Using dialog As New StarterApplisForm()
                                    dialog.ShowDialog(Me)
                                End Using
                            ElseIf String.Equals(first.MenuText.Trim(), "Started", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(clicked.Text.Trim(), "Started", StringComparison.OrdinalIgnoreCase) Then
                                StarterApplisForm.DemarrerApplications(Me)
                            ElseIf String.Equals(first.MenuText.Trim(), "Exemple", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(clicked.Text.Trim(), "Started", StringComparison.OrdinalIgnoreCase) Then
                                Using dialog As New StarterApplisForm()
                                    dialog.ShowDialog(Me)
                                End Using
                            ElseIf IsStarterApplis(clicked.Text, DirectCast(clicked.Tag, MenuRow).ActionName, first.MenuText) Then
                                Using dialog As New StarterApplisForm()
                                    dialog.ShowDialog(Me)
                                End Using
                            ElseIf IsSlideshow(clicked.Text, DirectCast(clicked.Tag, MenuRow).ActionName) Then
                                StartSlideshow()
                            ElseIf clicked.Text.StartsWith("Couleurs", StringComparison.OrdinalIgnoreCase) Then
                                ChangeColor(clicked.Text)
                            Else
                                Dim selected = DirectCast(clicked.Tag, MenuRow)
                                ShowInfo(selected.Info01, selected.Info02)
                                OpenPath(selected.Path, selected.ActionName, selected.SubText, selected.MenuText)
                            End If
                        End Sub
                        top.DropDownItems.Add(child)
                    End If
                Next
                If top.Text.StartsWith("Couleurs", StringComparison.OrdinalIgnoreCase) Then
                    AddHandler top.Click, Sub(sender, e) ChangeColor(top.Text)
                End If
                menuBar.Items.Add(top)
            Next
            AdjustMenuWidth()
            If menuBar.Items.Count = 0 Then
                ShowProblem("Aucun menu disponible pour " & GeneratedConfig.FormName & ".")
            End If
        Catch ex As Exception
            ShowProblem("Lecture de SQLite impossible : " & ex.Message)
        End Try
    End Sub

    Private Sub ShowCreateFileDialog()
        Using dialog As New Form With {
            .Text = "Création d'un nom de fichier",
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(700, 420),
            .MinimumSize = New Size(570, 360)
        }
            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 2,
                .RowCount = 7, .Padding = New Padding(14)
            }
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Dim terms As TextBox() = {
                New TextBox With {.Text = "VB"},
                New TextBox With {.Text = "CREATION"},
                New TextBox With {.Text = "Menu"}
            }
            For i = 0 To 2
                layout.Controls.Add(New Label With {.Text = "Terme_0" & (i + 1).ToString() & " :", .AutoSize = True}, 0, i)
                terms(i).Dock = DockStyle.Fill
                layout.Controls.Add(terms(i), 1, i)
            Next
            Dim extensionInput As New TextBox With {.Text = "txt", .Dock = DockStyle.Fill}
            layout.Controls.Add(New Label With {.Text = "Extension :", .AutoSize = True}, 0, 3)
            layout.Controls.Add(extensionInput, 1, 3)
            Dim preview As New TextBox With {.ReadOnly = True, .Dock = DockStyle.Fill}
            layout.Controls.Add(New Label With {.Text = "Nom du fichier :", .AutoSize = True}, 0, 4)
            layout.Controls.Add(preview, 1, 4)
            Dim folderRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False}
            Dim parentPath As New TextBox With {.Width = 330, .ReadOnly = True}
            Dim browse As New Button With {.Text = "Parcourir...", .AutoSize = True}
            folderRow.Controls.Add(parentPath)
            folderRow.Controls.Add(browse)
            layout.Controls.Add(New Label With {.Text = "Emplacement :", .AutoSize = True}, 0, 5)
            layout.Controls.Add(folderRow, 1, 5)
            Dim create As New Button With {.Text = "Créer le fichier", .AutoSize = True}
            layout.Controls.Add(create, 1, 6)
            Dim updatePreview As Action = Sub()
                Dim nowValue = DateTime.Now
                Dim prefix = String.Join(" ", terms.Select(Function(t) t.Text.Trim()).Where(Function(t) t.Length > 0))
                Dim suffix = extensionInput.Text.Trim().TrimStart("."c)
                preview.Text = prefix & " " & nowValue.ToString("yyyy-MM-dd") & "--" &
                    CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.GetAbbreviatedDayName(nowValue.DayOfWeek).TrimEnd("."c) &
                    "--" & nowValue.ToString("HH-mm-ss") & If(suffix.Length > 0, "." & suffix, "")
            End Sub
            For Each term In terms
                AddHandler term.TextChanged, Sub(sender, e) updatePreview()
            Next
            AddHandler extensionInput.TextChanged, Sub(sender, e) updatePreview()
            AddHandler browse.Click, Sub(sender, e)
                Using picker As New FolderBrowserDialog With {.Description = "Choisir le dossier de destination"}
                    If picker.ShowDialog(dialog) = DialogResult.OK Then parentPath.Text = picker.SelectedPath
                End Using
            End Sub
            AddHandler create.Click, Sub(sender, e)
                If String.IsNullOrWhiteSpace(parentPath.Text) Then
                    MessageBox.Show(dialog, "Choisissez un emplacement.", "Création de fichier")
                    Return
                End If
                Dim ext = extensionInput.Text.Trim().TrimStart("."c)
                If terms.Any(Function(t) String.IsNullOrWhiteSpace(t.Text) OrElse t.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) OrElse
                    ext.Length = 0 OrElse ext.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then
                    MessageBox.Show(dialog, "Renseignez les trois termes et une extension valide.", "Création de fichier")
                    Return
                End If
                updatePreview()
                Try
                    Dim destination = Path.Combine(parentPath.Text, preview.Text)
                    Using stream = New FileStream(destination, FileMode.CreateNew, FileAccess.Write)
                    End Using
                    MessageBox.Show(dialog, "Fichier créé : " & destination, "Création de fichier")
                    dialog.Close()
                Catch ex As Exception
                    MessageBox.Show(dialog, ex.Message, "Erreur de création", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub
            updatePreview()
            dialog.Controls.Add(layout)
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowCreateFolderDialog()
        Using dialog As New Form With {
            .Text = "Création d'un nom de dossier",
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(680, 380),
            .MinimumSize = New Size(560, 340)
        }
            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 6,
                .Padding = New Padding(14)
            }
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Dim terms As TextBox() = {
                New TextBox With {.Text = "VB"},
                New TextBox With {.Text = "CREATION"},
                New TextBox With {.Text = "Menu"}
            }
            For i = 0 To 2
                layout.Controls.Add(New Label With {.Text = "Terme_0" & (i + 1).ToString() & " :", .AutoSize = True}, 0, i)
                terms(i).Dock = DockStyle.Fill
                layout.Controls.Add(terms(i), 1, i)
            Next
            Dim preview As New TextBox With {.ReadOnly = True, .Dock = DockStyle.Fill}
            layout.Controls.Add(New Label With {.Text = "Nom du dossier :", .AutoSize = True}, 0, 3)
            layout.Controls.Add(preview, 1, 3)
            Dim folderRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False}
            Dim parentPath As New TextBox With {.Width = 330, .ReadOnly = True}
            Dim browse As New Button With {.Text = "Parcourir...", .AutoSize = True}
            folderRow.Controls.Add(parentPath)
            folderRow.Controls.Add(browse)
            layout.Controls.Add(New Label With {.Text = "Emplacement :", .AutoSize = True}, 0, 4)
            layout.Controls.Add(folderRow, 1, 4)
            Dim create As New Button With {.Text = "Créer le dossier", .AutoSize = True}
            layout.Controls.Add(create, 1, 5)
            Dim updatePreview As Action = Sub()
                Dim nowValue = DateTime.Now
                Dim prefix = String.Join(" ", terms.Select(Function(t) t.Text.Trim()).Where(Function(t) t.Length > 0))
                preview.Text = prefix & " " & nowValue.ToString("yyyy-MM-dd") & "--" &
                    CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.GetAbbreviatedDayName(nowValue.DayOfWeek).TrimEnd("."c) & "--" & nowValue.ToString("HH-mm-ss")
            End Sub
            For Each term In terms
                AddHandler term.TextChanged, Sub(sender, e) updatePreview()
            Next
            AddHandler browse.Click, Sub(sender, e)
                Using picker As New FolderBrowserDialog With {.Description = "Choisir le dossier parent"}
                    If picker.ShowDialog(dialog) = DialogResult.OK Then parentPath.Text = picker.SelectedPath
                End Using
            End Sub
            AddHandler create.Click, Sub(sender, e)
                If String.IsNullOrWhiteSpace(parentPath.Text) Then
                    MessageBox.Show(dialog, "Choisissez un emplacement.", "Création de dossier")
                    Return
                End If
                If terms.Any(Function(t) String.IsNullOrWhiteSpace(t.Text) OrElse
                    t.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) Then
                    MessageBox.Show(dialog, "Chaque terme doit être renseigné et ne pas contenir de caractère interdit.", "Création de dossier")
                    Return
                End If
                updatePreview()
                Try
                    Dim destination = Path.Combine(parentPath.Text, preview.Text)
                    If Directory.Exists(destination) Then
                        MessageBox.Show(dialog, "Ce dossier existe déjà : " & destination, "Création de dossier")
                        Return
                    End If
                    Directory.CreateDirectory(destination)
                    MessageBox.Show(dialog, "Dossier créé : " & destination, "Création de dossier")
                    dialog.Close()
                Catch ex As Exception
                    MessageBox.Show(dialog, ex.Message, "Erreur de création", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub
            updatePreview()
            dialog.Controls.Add(layout)
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowExampleDetails(item As MenuRow)
        Using details As New Form With {
            .Text = "Exemple — " & item.SubText,
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(720, 560),
            .MinimumSize = New Size(480, 360)
        }
            Dim rows As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .ColumnCount = 2,
                .Padding = New Padding(15)
            }
            rows.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
            rows.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Dim fields As (Label As String, Value As String)() = {
                ("Frm_Menu", item.FormName),
                ("N° Menu", item.MenuNumber),
                ("N° Sous-Menu", item.SubNumber),
                ("Menu", item.MenuText),
                ("Sous-Menu", item.SubText),
                ("Info_01", item.Info01),
                ("Info_02", item.Info02),
                ("Action", item.ActionName),
                ("Chemin", item.Path)
            }
            For Each entry In fields
                Dim caption As New Label With {
                    .Text = entry.Label & " :",
                    .AutoSize = True,
                    .Font = New Font(details.Font, FontStyle.Bold),
                    .Margin = New Padding(3, 9, 8, 9)
                }
                Dim value As New TextBox With {
                    .Text = entry.Value,
                    .ReadOnly = True,
                    .Multiline = True,
                    .Height = 45,
                    .Dock = DockStyle.Fill,
                    .ScrollBars = ScrollBars.Vertical
                }
                rows.RowCount += 1
                rows.Controls.Add(caption, 0, rows.RowCount - 1)
                rows.Controls.Add(value, 1, rows.RowCount - 1)
            Next
            Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
            scroll.Controls.Add(rows)
            details.Controls.Add(scroll)
            details.ShowDialog(Me)
        End Using
    End Sub

    Private Sub SaveMenuDatabase(sender As Object, e As EventArgs)
        Using dialog As New SaveFileDialog With {.Title = "Sauvegarder le menu autonome", .Filter = "SQLite (*.db)|*.db", .FileName = "CMenu-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".db"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Using source As New SqliteConnection("Data Source=" & databasePath & ";Mode=ReadOnly")
                    source.Open()
                    Using dest As New SqliteConnection("Data Source=" & dialog.FileName)
                        dest.Open()
                        source.BackupDatabase(dest)
                    End Using
                End Using
                MessageBox.Show(Me, "Sauvegarde terminée.")
            Catch ex As Exception
                MessageBox.Show(Me, "Sauvegarde impossible : " & ex.Message)
            End Try
        End Using
    End Sub

    Private Sub RestoreMenuDatabase(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {.Title = "Restaurer le menu autonome", .Filter = "SQLite (*.db)|*.db"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            If MessageBox.Show(Me, "Restaurer la base ? Une copie de sécurité sera créée.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
            Try
                Dim backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C-Menu-Sauvegardes")
                Directory.CreateDirectory(backupDir)
                Dim backupPath = Path.Combine(backupDir, "MenuAutonome-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".db")
                Using target As New SqliteConnection("Data Source=" & databasePath)
                    target.Open()
                    Using backup As New SqliteConnection("Data Source=" & backupPath)
                        backup.Open()
                        target.BackupDatabase(backup)
                    End Using
                    Using source As New SqliteConnection("Data Source=" & dialog.FileName & ";Mode=ReadOnly")
                        source.Open()
                        source.BackupDatabase(target)
                    End Using
                End Using
                LoadFromDatabase()
                MessageBox.Show(Me, "Restauration terminée.")
            Catch ex As Exception
                MessageBox.Show(Me, "Restauration impossible : " & ex.Message)
            End Try
        End Using
    End Sub

    Private Sub ChangeColor(menuText As String)
        Dim key = menuText.ToLowerInvariant()
        Using picker As New ColorDialog With {.FullOpen = True, .AnyColor = True}
            If key.Contains("libell") Then
                picker.Color = titleColor
            ElseIf key.Contains("bar") Then
                picker.Color = barColor
            ElseIf key.Contains("corps") Then
                picker.Color = bodyColor
            Else
                Return
            End If
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            If key.Contains("libell") Then
                titleColor = picker.Color
            ElseIf key.Contains("bar") Then
                barColor = picker.Color
            Else
                bodyColor = picker.Color
            End If
            ApplyColors()
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath))
                File.WriteAllLines(settingsPath, {titleColor.ToArgb().ToString(), barColor.ToArgb().ToString(), bodyColor.ToArgb().ToString()})
            Catch ex As Exception
                MessageBox.Show(Me, "Couleurs appliquées, mais sauvegarde impossible : " & ex.Message)
            End Try
        End Using
    End Sub

    Private Sub LoadColors()
        Try
            If File.Exists(settingsPath) Then
                Dim lines = File.ReadAllLines(settingsPath)
                If lines.Length >= 3 Then
                    titleColor = Color.FromArgb(Integer.Parse(lines(0)))
                    barColor = Color.FromArgb(Integer.Parse(lines(1)))
                    bodyColor = Color.FromArgb(Integer.Parse(lines(2)))
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub ApplyColors()
        menuBar.BackColor = barColor
        customTitle.BackColor = titleColor
        customTitleText.BackColor = titleColor
        BackColor = bodyColor
    End Sub

    Private Shared Function Value(reader As SqliteDataReader, index As Integer) As String
        Return If(reader.IsDBNull(index), "", Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture))
    End Function

    Private Sub ShowProblem(message As String)
        menuBar.Items.Add(New ToolStripLabel("Aucun menu"))
        MessageBox.Show(Me, message, "Menu autonome", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private ReadOnly slideshowTimer As New Timer With {.Interval = 8000}
    Private slideshowPanel As Panel
    Private slideshowPicture As PictureBox
    Private slideshowFiles As String() = Array.Empty(Of String)()
    Private slideshowIndex As Integer = 0
    Private ReadOnly slideshowRandom As New Random()
    Private originalWindowSize As Size
    Private Const SlideshowFolder As String = "C:\Menu\applications\Diaporama"

    Private ReadOnly appointmentLabel As New Label With {
        .AutoSize = False,
        .TextAlign = ContentAlignment.TopLeft,
        .Font = New Font("Segoe UI", 9, FontStyle.Bold),
        .Visible = False
    }
    Private Const AppointmentDatabase As String = "C:\Menu\applications\Rendez-vous\RendezVous.db"

    Private Sub RefreshAppointmentsDisplay()
        appointmentLabel.Visible = False
        If Not File.Exists(AppointmentDatabase) Then Return
        Try
            Dim lines As New List(Of String)()
            Using cn As New SqliteConnection("Data Source=" & AppointmentDatabase & ";Mode=ReadOnly")
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "SELECT Libelle, DateHeure, Note FROM RendezVous WHERE DateHeure >= $now ORDER BY DateHeure"
                    cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim d As Date
                            If Not Date.TryParseExact(reader.GetString(1), "yyyy-MM-dd HH:mm:ss",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, d) Then Continue While
                            lines.Add(reader.GetString(0).Trim() & Environment.NewLine &
                                "le " & d.ToString("dddd d MMMM yyyy 'à' HH'h'mm", CultureInfo.GetCultureInfo("fr-FR")) &
                                If(reader.IsDBNull(2) OrElse String.IsNullOrWhiteSpace(If(reader.IsDBNull(2), "", reader.GetString(2))), "",
                                   Environment.NewLine & reader.GetString(2).Trim()))
                        End While
                    End Using
                End Using
            End Using
            If lines.Count > 0 Then
                appointmentLabel.Text = String.Join(Environment.NewLine & Environment.NewLine, lines)
                Dim widthNeeded As Integer = 0
                For Each line In appointmentLabel.Text.Split({Environment.NewLine}, StringSplitOptions.None)
                    widthNeeded = Math.Max(widthNeeded, TextRenderer.MeasureText(line, appointmentLabel.Font).Width)
                Next
                appointmentLabel.Width = widthNeeded + 20
                appointmentLabel.Height = (lines.Sum(Function(x) x.Split({Environment.NewLine}, StringSplitOptions.None).Length) +
                    (lines.Count - 1)) * (appointmentLabel.Font.Height + 3) + 12
                appointmentLabel.Visible = True
            End If
        Catch ex As SqliteException
        Catch ex As IOException
        End Try
        AdjustMenuWidth()
    End Sub

    Private Sub PositionSlideshow()
        If slideshowPanel Is Nothing Then Return
        slideshowPanel.Dock = DockStyle.None
        Dim topOfBody = menuBar.Bottom
        
        Dim offsetPixels As Integer
        Using graphics = CreateGraphics()
            offsetPixels = CInt(Math.Round(graphics.DpiY * 0.3 / 2.54))
        End Using
        slideshowPanel.Location = New Point(Math.Max(0, (ClientSize.Width - slideshowPanel.Width) \ 2),
                                            topOfBody + offsetPixels)
        slideshowPanel.BringToFront()
    End Sub

    Private Sub PositionAppointments()
        If Not appointmentLabel.Visible Then Return
        Dim topOfBody = menuBar.Bottom
        Dim offsetPixels As Integer
        Using graphics = CreateGraphics()
            offsetPixels = CInt(Math.Round(graphics.DpiY * 0.5 / 2.54))
        End Using
        Dim topPosition = topOfBody + offsetPixels
        If nextEventLabel.Visible Then topPosition = Math.Max(topPosition, nextEventLabel.Bottom + 8)
        appointmentLabel.Location = New Point(12, topPosition)
        appointmentLabel.BringToFront()
        Dim neededHeight = appointmentLabel.Bottom + 12 + (Height - ClientSize.Height)
        If Height < neededHeight Then Height = neededHeight
    End Sub

    Private ReadOnly nextEventLabel As New Label With {
        .ForeColor = Color.Black,
        .AutoSize = False,
        .TextAlign = ContentAlignment.MiddleLeft,
        .Padding = New Padding(4, 4, 8, 4),
        .Font = New Font("Segoe UI", 9, FontStyle.Bold),
        .Visible = False
    }
    Private Sub PositionNextEvent()
        If Not nextEventLabel.Visible Then Return
        Dim offsetPixels As Integer
        Using graphics = CreateGraphics()
            offsetPixels = CInt(Math.Round(graphics.DpiY * 0.5 / 2.54))
        End Using
        Dim topOfBody = menuBar.Bottom
        
        nextEventLabel.Location = New Point(12,
                                            topOfBody + offsetPixels)
        nextEventLabel.Height = Math.Max(28, nextEventLabel.Text.Split({Environment.NewLine}, StringSplitOptions.None).Length * (nextEventLabel.Font.Height + 3) + 10)
        nextEventLabel.BringToFront()
        Dim neededHeight = nextEventLabel.Bottom + 12 + (Height - ClientSize.Height)
        If Height < neededHeight Then Height = neededHeight
        PositionAppointments()
    End Sub

    Private Const AnniversaryDatabase As String = "C:\Menu\applications\Anniversaire\Anniversaires.db"

    Private Sub RefreshNextEvent()
        nextEventLabel.Visible = False
        If Not File.Exists(AnniversaryDatabase) Then
            AdjustMenuWidth()
            Return
        End If
        Try
            Dim nextDate As Date = Date.MaxValue
            Dim nextTexts As New List(Of String)()
            Using cn As New SqliteConnection("Data Source=" & AnniversaryDatabase & ";Mode=ReadOnly")
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "SELECT Nom, Prenom, Naissance, Fete, ANoter FROM Evenements"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim person = reader.GetString(1).Trim() & " " & reader.GetString(0).Trim()
                            For i As Integer = 2 To 4
                                If reader.IsDBNull(i) Then Continue For
                                Dim stored As Date
                                If Not Date.TryParseExact(reader.GetString(i), "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, stored) Then Continue For
                                Dim year = Date.Today.Year
                                Dim occurrence = New Date(year, stored.Month, Math.Min(stored.Day, Date.DaysInMonth(year, stored.Month)))
                                If occurrence < Date.Today Then
                                    year += 1
                                    occurrence = New Date(year, stored.Month, Math.Min(stored.Day, Date.DaysInMonth(year, stored.Month)))
                                End If
                                If occurrence <= nextDate Then
                                    If occurrence < nextDate Then
                                        nextDate = occurrence
                                        nextTexts.Clear()
                                    End If
                                    Dim kind = If(i = 2, "Anniversaire", If(i = 3, "Fête", "Date à noter"))
                                    If i = 2 Then
                                        Dim age = occurrence.Year - stored.Year
                                        kind = "Anniversaire des " & age.ToString() & " ans"
                                    End If
                                    If i = 2 Then
                                        nextTexts.Add(kind & " de" & Environment.NewLine & person & Environment.NewLine &
                                            "le " & occurrence.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")))
                                    Else
                                        nextTexts.Add(kind & " de " & person & " le " &
                                            occurrence.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")))
                                    End If
                                End If
                            Next
                        End While
                    End Using
                End Using
            End Using
            If nextTexts.Count > 0 Then
                nextEventLabel.Text = String.Join(Environment.NewLine & Environment.NewLine, nextTexts)
                nextEventLabel.Width = nextEventLabel.Text.Split({Environment.NewLine}, StringSplitOptions.None).
                    Max(Function(line) TextRenderer.MeasureText(line, nextEventLabel.Font).Width) + 28
                nextEventLabel.Visible = True
                PositionNextEvent()
            End If
        Catch ex As SqliteException
            ' Base absente de table ou temporairement indisponible : ne pas bloquer le menu.
        Catch ex As IOException
        End Try
        AdjustMenuWidth()
    End Sub

    Private Sub AdjustMenuWidth()
        ' La largeur suit le libellé du formulaire ou la somme des menus visibles.
        Dim labelText As String = Text
        Dim labelFont As Font = Font
        Dim labelWidth = TextRenderer.MeasureText(labelText, labelFont).Width + 192 + 24
        Dim menusWidth As Integer = menuBar.Padding.Horizontal + 16
        For Each entry As ToolStripItem In menuBar.Items
            menusWidth += TextRenderer.MeasureText(entry.Text, menuBar.Font).Width + 30
        Next
        Dim contentWidth = Math.Max(labelWidth, menusWidth)
        If slideshowPanel IsNot Nothing Then contentWidth = Math.Max(contentWidth, slideshowPanel.Width + 24)
        If nextEventLabel.Visible Then contentWidth += nextEventLabel.Width + 12
        Dim eventWidth = Math.Max(If(nextEventLabel.Visible, nextEventLabel.Width, 0), If(appointmentLabel.Visible, appointmentLabel.Width, 0))
        If eventWidth > 0 Then contentWidth = Math.Max(contentWidth, eventWidth + 24)
        Dim frameWidth = Width - ClientSize.Width
        Dim targetWidth = contentWidth + frameWidth
        Dim availableWidth = Screen.FromControl(Me).WorkingArea.Width
        targetWidth = Math.Min(targetWidth, availableWidth)
        MinimumSize = New Size(Math.Min(targetWidth, availableWidth), MinimumSize.Height)
        Width = targetWidth
        PositionNextEvent()
        PositionAppointments()
        PositionSlideshow()
    End Sub

    Private Sub StartSlideshow(Optional silent As Boolean = False)
        Try
            If Not Directory.Exists(SlideshowFolder) Then
                Directory.CreateDirectory(SlideshowFolder)
                If Not silent Then
                Dim opus = "C:\Program Files\GPSoftware\Directory Opus\x64\dopus.exe"
                If File.Exists(opus) Then
                    Dim launch As New ProcessStartInfo(opus) With {.UseShellExecute = True}
                    launch.ArgumentList.Add(SlideshowFolder)
                    Process.Start(launch)
                Else
                    Dim launch As New ProcessStartInfo("explorer.exe") With {.UseShellExecute = True}
                    launch.ArgumentList.Add(SlideshowFolder)
                    Process.Start(launch)
                End If
                End If
            End If
            If slideshowPanel Is Nothing Then
                originalWindowSize = Size
                Dim pxWidth As Integer
                Dim pxHeight As Integer
                Using graphics = CreateGraphics()
                    pxWidth = CInt(Math.Round(graphics.DpiX * 4.0 / 2.54))
                    pxHeight = CInt(Math.Round(graphics.DpiY * 4.0 / 2.54))
                End Using
                slideshowPanel = New Panel With {
                    .Size = New Size(pxWidth, pxHeight),
                    .Dock = DockStyle.None,
                    .BorderStyle = BorderStyle.None,
                    .BackColor = Color.Transparent
                }
                slideshowPicture = New PictureBox With {
                    .Dock = DockStyle.Fill,
                    .SizeMode = PictureBoxSizeMode.Zoom,
                    .BackColor = Color.Transparent
                }
                slideshowPanel.Controls.Add(slideshowPicture)
                Controls.Add(slideshowPanel)
                slideshowPanel.BringToFront()
                AdjustMenuWidth()
                PositionSlideshow()
                Height = Math.Max(Height, pxHeight + 70)
                PositionSlideshow()
                AddHandler slideshowTimer.Tick, Sub(sender, e) NextSlideshowImage()
            End If
            slideshowFiles = Directory.GetFiles(SlideshowFolder).
                Where(Function(f) {".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"}.
                Contains(Path.GetExtension(f).ToLowerInvariant())).
                OrderBy(Function(f) f, StringComparer.CurrentCultureIgnoreCase).ToArray()
            ShuffleSlideshowFiles()
            slideshowIndex = 0
            NextSlideshowImage()
            slideshowTimer.Start()
            If slideshowFiles.Length = 0 AndAlso Not silent Then
                MessageBox.Show(Me, "Déposez des photos dans : " & SlideshowFolder,
                                "Diaporama", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show(Me, "Impossible de démarrer le diaporama : " & ex.Message,
                            "Diaporama", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub ShuffleSlideshowFiles()
        For i As Integer = slideshowFiles.Length - 1 To 1 Step -1
            Dim j = slideshowRandom.Next(i + 1)
            Dim temp = slideshowFiles(i)
            slideshowFiles(i) = slideshowFiles(j)
            slideshowFiles(j) = temp
        Next
    End Sub

    Private Shared Function CorrectPhotoOrientation(sourceImage As Image) As Bitmap
        ' Respecter l'orientation EXIF des photos prises avec un téléphone.
        Const orientationId As Integer = &H112
        Dim result As New Bitmap(sourceImage)
        Try
            If sourceImage.PropertyIdList.Contains(orientationId) Then
                Dim orientation = sourceImage.GetPropertyItem(orientationId)
                If orientation IsNot Nothing AndAlso orientation.Value IsNot Nothing AndAlso orientation.Value.Length >= 2 Then
                    Dim value = BitConverter.ToUInt16(orientation.Value, 0)
                    Dim rotation As RotateFlipType = RotateFlipType.RotateNoneFlipNone
                    Select Case value
                        Case 2 : rotation = RotateFlipType.RotateNoneFlipX
                        Case 3 : rotation = RotateFlipType.Rotate180FlipNone
                        Case 4 : rotation = RotateFlipType.Rotate180FlipX
                        Case 5 : rotation = RotateFlipType.Rotate90FlipX
                        Case 6 : rotation = RotateFlipType.Rotate90FlipNone
                        Case 7 : rotation = RotateFlipType.Rotate270FlipX
                        Case 8 : rotation = RotateFlipType.Rotate270FlipNone
                    End Select
                    result.RotateFlip(rotation)
                End If
            End If
        Catch
            ' En l'absence de métadonnées valides, garder la photo originale.
        End Try
        Return result
    End Function

    Private Sub NextSlideshowImage()
        If slideshowFiles.Length = 0 OrElse slideshowPicture Is Nothing Then Return
        Dim attempts = slideshowFiles.Length
        While attempts > 0
            If slideshowIndex >= slideshowFiles.Length Then
                ShuffleSlideshowFiles()
                slideshowIndex = 0
            End If
            Dim filePath = slideshowFiles(slideshowIndex)
            slideshowIndex += 1
            attempts -= 1
            Try
                Using stream = New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using sourceImage = Image.FromStream(stream)
                        Dim replacement = CorrectPhotoOrientation(sourceImage)
                        Dim previous = slideshowPicture.Image
                        slideshowPicture.Image = replacement
                        If previous IsNot Nothing Then previous.Dispose()
                    End Using
                End Using
                Return
            Catch
                ' Ignorer les images illisibles et passer à la suivante.
            End Try
        End While
    End Sub

    Private Shared Function IsSlideshow(submenuText As String, actionName As String) As Boolean
        Return String.Equals(If(submenuText, "").Trim(), "Diaporama", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(If(actionName, "").Trim(), "DIAPORAMA", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Async Sub ShowInfo(info1 As String, info2 As String)
        ' La fenêtre d'informations ne s'affiche que si les deux champs sont renseignés.
        If String.IsNullOrWhiteSpace(info1) OrElse String.IsNullOrWhiteSpace(info2) Then
            If infoWindow IsNot Nothing AndAlso Not infoWindow.IsDisposed Then infoWindow.Hide()
            Return
        End If
        If infoWindow Is Nothing OrElse infoWindow.IsDisposed Then
            Dim dpiX As Single
            Dim dpiY As Single
            Using graphics = CreateGraphics()
                dpiX = graphics.DpiX
                dpiY = graphics.DpiY
            End Using
            infoWindow = New Form With {
                .Text = "Informations du sous-menu",
                .Size = New Size(CInt(8 / 2.54 * dpiX), CInt(4 / 2.54 * dpiY)),
                .StartPosition = FormStartPosition.Manual,
                .Location = New Point(80, 80),
                .TopMost = True
            }
            infoText = New TextBox With {
                .Multiline = True, .ReadOnly = True, .Dock = DockStyle.Fill,
                .ScrollBars = ScrollBars.Vertical, .Font = New Font("Segoe UI", 10)
            }
            infoWindow.Controls.Add(infoText)
        End If
        infoText.Text = If(info1, "") & Environment.NewLine & If(info2, "")
        Try
            ' Deux copies distinctes pour l'historique Windows (Win+V).
            ' Une seule valeur peut rester active pour Ctrl+V : Info02 est la dernière.
            If Not String.IsNullOrWhiteSpace(info1) Then Clipboard.SetText(info1)
            If Not String.IsNullOrWhiteSpace(info1) AndAlso Not String.IsNullOrWhiteSpace(info2) Then
                Await System.Threading.Tasks.Task.Delay(1000)
            End If
            If Not String.IsNullOrWhiteSpace(info2) Then Clipboard.SetText(info2)
        Catch ex As Exception
            MessageBox.Show(Me, "Impossible de copier les informations dans le presse-papiers : " & ex.Message,
                            "Presse-papiers", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
        If Not infoWindow.Visible Then infoWindow.Show(Me)
        infoWindow.BringToFront()
    End Sub

    Private Shared Function IsStarterApplis(label As String, actionName As String, Optional parentLabel As String = "") As Boolean
        Dim normalize As Func(Of String, String) =
            Function(value) New String(If(value, "").ToLowerInvariant().Where(Function(ch) Char.IsLetterOrDigit(ch)).ToArray())
        Dim item = normalize(label)
        Dim action = normalize(actionName)
        Dim parent = normalize(parentLabel)
        Return item.Contains("starterapplis") OrElse action.Contains("starterapplis") OrElse
               item = "startersauvegarde" OrElse action = "startersauvegarde" OrElse
               (item = "started" AndAlso (parent = "started" OrElse parent = "exemple")) OrElse
               (action = "system_started" AndAlso (parent = "started" OrElse parent = "exemple")) OrElse
               ((parent = "starter" OrElse parent = "starterapplis") AndAlso item = "sauvegarde")
    End Function

    Private Sub OpenPath(pathValue As String, actionName As String, Optional menuLabel As String = "", Optional parentLabel As String = "")
        If String.Equals(parentLabel.Trim(), "Started", StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(menuLabel.Trim(), "Started", StringComparison.OrdinalIgnoreCase) Then
            StarterApplisForm.DemarrerApplications(Me)
            Return
        End If
        If IsStarterApplis(menuLabel, actionName, parentLabel) Then
            Using dialog As New StarterApplisForm()
                dialog.ShowDialog(Me)
            End Using
            Return
        End If
        If String.IsNullOrWhiteSpace(pathValue) Then
            MessageBox.Show(Me, "Aucun chemin défini pour ce sous-menu.", "Menu autonome")
            Return
        End If
        Try
            Dim destination = Environment.ExpandEnvironmentVariables(pathValue.Trim())
            If String.Equals(If(actionName, "").Trim(), "DOSSIER-OUVRE", StringComparison.OrdinalIgnoreCase) OrElse Directory.Exists(destination) Then
                If Not Directory.Exists(destination) Then Throw New DirectoryNotFoundException("Dossier introuvable : " & destination)
                Dim opus = "C:\Program Files\GPSoftware\Directory Opus\x64\dopus.exe"
                If File.Exists(opus) Then
                    Dim launch As New ProcessStartInfo(opus) With {.UseShellExecute = True}
                    launch.ArgumentList.Add(destination)
                    Process.Start(launch)
                Else
                    Dim explorerLaunch As New ProcessStartInfo("explorer.exe") With {.UseShellExecute = True}
                    explorerLaunch.ArgumentList.Add(destination)
                    Process.Start(explorerLaunch)
                End If
            Else
                Process.Start(New ProcessStartInfo(destination) With {.UseShellExecute = True})
            End If
        Catch ex As Exception
            MessageBox.Show(Me, "Impossible d'ouvrir : " & pathValue &
                            Environment.NewLine & ex.Message, "Menu autonome",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Class MenuRow
        Public Property FormName As String = ""
        Public Property MenuNumber As String = ""
        Public Property SubNumber As String = ""
        Public Property MenuText As String = ""
        Public Property SubText As String = ""
        Public Property Path As String = ""
        Public Property ActionName As String = ""
        Public Property Info01 As String = ""
        Public Property Info02 As String = ""
    End Class
End Class


Public Class FrmAnniversaire
    Inherits Form

    Private ReadOnly databaseFile As String = Path.Combine("C:\", "Menu", "applications", "Anniversaire", "Anniversaires.db")
    Private ReadOnly nom As New TextBox()
    Private ReadOnly prenom As New TextBox()
    Private ReadOnly naissance As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
    Private ReadOnly fete As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
    Private ReadOnly noter As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
    Private ReadOnly libelleANoter As New TextBox()
    Private ReadOnly eventsGrid As New DataGridView With {
        .Dock = DockStyle.Fill, .ReadOnly = True, .AllowUserToAddRows = False,
        .AllowUserToDeleteRows = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        .SelectionMode = DataGridViewSelectionMode.FullRowSelect
    }

    Public Sub New()
        Text = "Anniversaires et événements à venir"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(800, 540)
        MinimumSize = New Size(650, 430)
        Font = New Font("Segoe UI", 10)
        Dim entry As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 240, .ColumnCount = 2, .RowCount = 6, .Padding = New Padding(12)}
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180))
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim labels = {"Nom", "Prénom", "Date de naissance", "Date de fête", "Date à noter", "Libellé de la date à noter"}
        Dim inputControls As Control() = {nom, prenom, naissance, fete, noter, libelleANoter}
        For i = 0 To labels.Length - 1
            entry.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F / labels.Length))
            entry.Controls.Add(New Label With {.Text = labels(i), .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, i)
            inputControls(i).Dock = DockStyle.Fill
            entry.Controls.Add(inputControls(i), 1, i)
        Next
        Dim saveButton As New Button With {.Text = "Enregistrer", .AutoSize = True}
        AddHandler saveButton.Click, AddressOf SaveEvent
        Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 43, .Padding = New Padding(12, 5, 0, 0)}
        toolbar.Controls.Add(saveButton)
        Me.Controls.Add(eventsGrid)
        Me.Controls.Add(toolbar)
        Me.Controls.Add(entry)
        Directory.CreateDirectory(Path.GetDirectoryName(databaseFile))
        Using cn As New SqliteConnection("Data Source=" & databaseFile)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Evenements (Id INTEGER PRIMARY KEY AUTOINCREMENT, Nom TEXT NOT NULL, Prenom TEXT NOT NULL, Naissance TEXT, Fete TEXT, ANoter TEXT, LibelleANoter TEXT)"
                cmd.ExecuteNonQuery()
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Evenements') WHERE name = 'LibelleANoter'"
                If Convert.ToInt32(cmd.ExecuteScalar()) = 0 Then
                    cmd.CommandText = "ALTER TABLE Evenements ADD COLUMN LibelleANoter TEXT"
                    cmd.ExecuteNonQuery()
                End If
            End Using
        End Using
        RefreshEvents()
    End Sub

    Private Sub SaveEvent(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(nom.Text) OrElse String.IsNullOrWhiteSpace(prenom.Text) Then
            MessageBox.Show(Me, "Saisissez le nom et le prénom.", "Anniversaire")
            Return
        End If
        Using cn As New SqliteConnection("Data Source=" & databaseFile)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "INSERT INTO Evenements (Nom, Prenom, Naissance, Fete, ANoter, LibelleANoter) VALUES ($nom, $prenom, $naissance, $fete, $noter, $libelle)"
                cmd.Parameters.AddWithValue("$nom", nom.Text.Trim())
                cmd.Parameters.AddWithValue("$prenom", prenom.Text.Trim())
                cmd.Parameters.AddWithValue("$naissance", If(naissance.Checked, naissance.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("$fete", If(fete.Checked, fete.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("$noter", If(noter.Checked, noter.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("$libelle", libelleANoter.Text.Trim())
                cmd.ExecuteNonQuery()
            End Using
        End Using
        nom.Clear()
        prenom.Clear()
        libelleANoter.Clear()
        naissance.Checked = False
        fete.Checked = False
        noter.Checked = False
        RefreshEvents()
    End Sub

    Private Shared Function NextOccurrence(value As Date) As Date
        Dim year = Date.Today.Year
        Dim day = Math.Min(value.Day, Date.DaysInMonth(year, value.Month))
        Dim result = New Date(year, value.Month, day)
        If result < Date.Today Then
            year += 1
            result = New Date(year, value.Month, Math.Min(value.Day, Date.DaysInMonth(year, value.Month)))
        End If
        Return result
    End Function

    Private Sub RefreshEvents()
        Dim upcoming As New List(Of (DateValue As Date, Person As String, Kind As String))()
        Using cn As New SqliteConnection("Data Source=" & databaseFile)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT Nom, Prenom, Naissance, Fete, ANoter, LibelleANoter FROM Evenements"
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim person = reader.GetString(1) & " " & reader.GetString(0)
                        For i = 2 To 4
                            If reader.IsDBNull(i) Then Continue For
                            Dim dateValue As Date
                            If Date.TryParseExact(reader.GetString(i), "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, dateValue) Then
                                Dim kind = If(i = 2, "Anniversaire", If(i = 3, "Fête", "Date à noter"))
                                If i = 4 AndAlso Not reader.IsDBNull(5) AndAlso Not String.IsNullOrWhiteSpace(reader.GetString(5)) Then
                                    kind = reader.GetString(5)
                                End If
                                upcoming.Add((NextOccurrence(dateValue), person, kind))
                            End If
                        Next
                    End While
                End Using
            End Using
        End Using
        eventsGrid.DataSource = upcoming.OrderBy(Function(x) x.DateValue).
            Select(Function(x) New With {
                .DateEvenement = x.DateValue.ToString("dd/MM/yyyy"),
                .Dans = (x.DateValue - Date.Today).Days.ToString() & " jour(s)",
                .Personne = x.Person,
                .Evenement = x.Kind
            }).ToList()
        If eventsGrid.Columns.Count = 4 Then
            eventsGrid.Columns(0).HeaderText = "Date"
            eventsGrid.Columns(1).HeaderText = "Dans"
            eventsGrid.Columns(2).HeaderText = "Nom et prénom"
            eventsGrid.Columns(3).HeaderText = "Événement"
        End If
    End Sub
End Class

Public Class FrmRendezVous
    Inherits Form

    Private Const DbPath As String = "C:\Menu\applications\Rendez-vous\RendezVous.db"
    Private ReadOnly libelle As New TextBox()
    Private ReadOnly dateHeure As New DateTimePicker With {
        .Format = DateTimePickerFormat.Custom, .CustomFormat = "dd/MM/yyyy HH:mm",
        .ShowUpDown = False, .Width = 220
    }
    Private ReadOnly note As New TextBox With {.Multiline = True, .ScrollBars = ScrollBars.Vertical, .Height = 70}
    Private ReadOnly upcoming As New DataGridView With {
        .Dock = DockStyle.Fill, .ReadOnly = True, .AllowUserToAddRows = False,
        .AllowUserToDeleteRows = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    }

    Public Sub New()
        Text = "Rendez-vous"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(760, 560)
        MinimumSize = New Size(620, 450)
        Font = New Font("Segoe UI", 10)
        Dim entry As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 215, .ColumnCount = 2, .RowCount = 3,
            .Padding = New Padding(12)
        }
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145))
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        entry.RowStyles.Add(New RowStyle(SizeType.Absolute, 45))
        entry.RowStyles.Add(New RowStyle(SizeType.Absolute, 45))
        entry.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim labels = {"Libellé", "Date et heure", "Note"}
        Dim inputs As Control() = {libelle, dateHeure, note}
        For i = 0 To labels.Length - 1
            entry.Controls.Add(New Label With {.Text = labels(i), .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, i)
            inputs(i).Dock = DockStyle.Fill
            entry.Controls.Add(inputs(i), 1, i)
        Next
        Dim save As New Button With {.Text = "Enregistrer", .AutoSize = True}
        AddHandler save.Click, AddressOf SaveAppointment
        Dim bar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 45, .Padding = New Padding(12, 5, 0, 0)}
        bar.Controls.Add(save)
        Me.Controls.Add(upcoming)
        Me.Controls.Add(bar)
        Me.Controls.Add(entry)
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath))
        Using cn As New SqliteConnection("Data Source=" & DbPath)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS RendezVous (Id INTEGER PRIMARY KEY AUTOINCREMENT, Libelle TEXT NOT NULL, DateHeure TEXT NOT NULL, Note TEXT)"
                cmd.ExecuteNonQuery()
            End Using
        End Using
        RefreshAppointments()
    End Sub

    Private Sub SaveAppointment(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(libelle.Text) Then
            MessageBox.Show(Me, "Saisissez le libellé du rendez-vous.", "Rendez-vous")
            Return
        End If
        Using cn As New SqliteConnection("Data Source=" & DbPath)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "INSERT INTO RendezVous (Libelle, DateHeure, Note) VALUES ($libelle, $date, $note)"
                cmd.Parameters.AddWithValue("$libelle", libelle.Text.Trim())
                cmd.Parameters.AddWithValue("$date", dateHeure.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                cmd.Parameters.AddWithValue("$note", note.Text.Trim())
                cmd.ExecuteNonQuery()
            End Using
        End Using
        libelle.Clear()
        note.Clear()
        RefreshAppointments()
    End Sub

    Private Sub RefreshAppointments()
        Dim rows As New List(Of (DateValue As Date, Title As String, Notes As String))()
        Using cn As New SqliteConnection("Data Source=" & DbPath)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT Libelle, DateHeure, Note FROM RendezVous WHERE DateHeure >= $now ORDER BY DateHeure"
                cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim d As Date
                        If Date.TryParseExact(reader.GetString(1), "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, d) Then
                            rows.Add((d, reader.GetString(0), If(reader.IsDBNull(2), "", reader.GetString(2))))
                        End If
                    End While
                End Using
            End Using
        End Using
        upcoming.DataSource = rows.Select(Function(x) New With {
            .Libelle = x.Title,
            .DateEtHeure = x.DateValue.ToString("dddd d MMMM yyyy 'à' HH'h'mm", CultureInfo.GetCultureInfo("fr-FR")),
            .Note = x.Notes
        }).ToList()
        If upcoming.Columns.Count = 3 Then
            upcoming.Columns(0).HeaderText = "Libellé"
            upcoming.Columns(1).HeaderText = "Date et heure"
        End If
    End Sub
End Class


' Formulaire Starter-Applis inclus dans le menu autonome généré.
Public Class StarterApplisForm
    Inherits Form

    Private ReadOnly dbPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "CMenu.db")
    Private ReadOnly grille As New DataGridView()
    Private ReadOnly actionNom As New TextBox()
    Private ReadOnly saisiele As New TextBox()
    Private ReadOnly modifierle As New TextBox()
    Private ReadOnly sauvegarde As New TextBox()
    Private ReadOnly libelle As New TextBox()
    Private ReadOnly applicationNom As New TextBox()
    Private ReadOnly chemin As New TextBox()
    Private ReadOnly priorite As New NumericUpDown With {.Minimum = 1, .Maximum = 99999, .Value = 100}
    Private selectedId As Long = 0

    Public Sub New()
        Text = "Starter-Applis — Gestion des applications"
        Size = New Size(1050, 650)
        MinimumSize = New Size(750, 500)
        StartPosition = FormStartPosition.CenterParent
        Font = New Font("Segoe UI", 10)
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(8)}
        AddButton(buttons, "Nouveau", AddressOf Nouveau)
        AddButton(buttons, "Ajouter", AddressOf Ajouter)
        AddButton(buttons, "Modifier", AddressOf Modifier)
        AddButton(buttons, "Supprimer", AddressOf Supprimer)
        AddButton(buttons, "Sauvegarder", AddressOf Sauvegarder)
        AddButton(buttons, "Parcourir...", AddressOf Parcourir)
        AddButton(buttons, "Démarrer dans l’ordre", AddressOf Demarrer)
        AddButton(buttons, "Importer depuis C-Menu", AddressOf ImporterDepuisMenu)
        Dim editor As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 252, .ColumnCount = 2, .RowCount = 8, .Padding = New Padding(12)}
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        AddField(editor, "Saisiele", saisiele, 0, True)
        AddField(editor, "Modifierle", modifierle, 1, True)
        AddField(editor, "Sauvegarde", sauvegarde, 2, True)
        AddField(editor, "Libellé", libelle, 3, False)
        AddField(editor, "Application", applicationNom, 4, False)
        AddField(editor, "ACTION", actionNom, 5, False)
        AddField(editor, "Lien", chemin, 6, False)
        editor.Controls.Add(New Label With {.Text = "Priorité (1 = premier)", .Dock = DockStyle.Fill}, 0, 7)
        priorite.Dock = DockStyle.Fill
        editor.Controls.Add(priorite, 1, 7)
        grille.Dock = DockStyle.Fill
        grille.ReadOnly = True
        grille.AllowUserToAddRows = False
        grille.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grille.MultiSelect = False
        grille.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        AddHandler grille.SelectionChanged, AddressOf SelectionChanged
        Controls.Add(grille)
        Controls.Add(editor)
        Controls.Add(buttons)
        Try
            InitDb()
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Starter-Applis", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ImporterDepuisMenu(sender As Object, e As EventArgs)
        Using dialog As New Form With {.Text = "Choisir un enregistrement C-Menu", .Size = New Size(1000, 560), .StartPosition = FormStartPosition.CenterParent}
            Dim filters As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(8)}
            Dim fMenu As New TextBox With {.Width = 160, .PlaceholderText = "Filtre Menu"}
            Dim fSub As New TextBox With {.Width = 160, .PlaceholderText = "Filtre Sous-Menu"}
            Dim fApp As New TextBox With {.Width = 160, .PlaceholderText = "Filtre Application"}
            filters.Controls.AddRange(New Control() {fMenu, fSub, fApp})
            Dim sourceGrid As New DataGridView With {.Dock = DockStyle.Fill, .ReadOnly = True, .SelectionMode = DataGridViewSelectionMode.FullRowSelect, .MultiSelect = False, .AllowUserToAddRows = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
            Dim bottom As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 48}
            Dim choose As New Button With {.Text = "Insérer dans Started", .AutoSize = True}
            bottom.Controls.Add(choose)
            dialog.Controls.Add(sourceGrid)
            dialog.Controls.Add(bottom)
            dialog.Controls.Add(filters)
            Dim reload As Action = Sub()
                Try
                    Using cn = Connexion()
                        cn.Open()
                        Using cmd = cn.CreateCommand()
                            cmd.CommandText = "SELECT Id, Menu, SousMenu AS [Sous-Menu], Application, Action, Chemin AS Lien FROM Menus WHERE Menu LIKE $m AND SousMenu LIKE $s AND Application LIKE $a ORDER BY CAST(NumMenu AS INTEGER), CAST(NumSousMenu AS INTEGER)"
                            cmd.Parameters.AddWithValue("$m", "%" & fMenu.Text & "%")
                            cmd.Parameters.AddWithValue("$s", "%" & fSub.Text & "%")
                            cmd.Parameters.AddWithValue("$a", "%" & fApp.Text & "%")
                            Dim table As New DataTable()
                            Using reader = cmd.ExecuteReader()
                                table.Load(reader)
                            End Using
                            sourceGrid.DataSource = table
                            If sourceGrid.Columns.Contains("Id") Then sourceGrid.Columns("Id").Visible = False
                        End Using
                    End Using
                Catch ex As Exception
                    MessageBox.Show(dialog, "Lecture des menus impossible : " & ex.Message)
                End Try
            End Sub
            AddHandler fMenu.TextChanged, Sub(s, args) reload()
            AddHandler fSub.TextChanged, Sub(s, args) reload()
            AddHandler fApp.TextChanged, Sub(s, args) reload()
            Dim selectRow As Action = Sub()
                If sourceGrid.CurrentRow Is Nothing Then Return
                Dim row = TryCast(sourceGrid.CurrentRow.DataBoundItem, DataRowView)
                If row Is Nothing Then Return
                Dim link = Convert.ToString(row("Lien"))
                If String.IsNullOrWhiteSpace(link) Then
                    MessageBox.Show(dialog, "Cet enregistrement n'a pas de lien à ouvrir.")
                    Return
                End If
                Nouveau(Nothing, EventArgs.Empty)
                libelle.Text = Convert.ToString(row("Sous-Menu"))
                applicationNom.Text = Convert.ToString(row("Application"))
                actionNom.Text = Convert.ToString(row("Action"))
        priorite.Value = Math.Max(priorite.Minimum, Math.Min(priorite.Maximum, Convert.ToDecimal(row("Priorité"))))
                chemin.Text = link
                Ajouter(Nothing, EventArgs.Empty)
                dialog.Close()
            End Sub
            AddHandler choose.Click, Sub(s, args) selectRow()
            AddHandler sourceGrid.CellDoubleClick, Sub(s, args) selectRow()
            reload()
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub AddButton(panel As FlowLayoutPanel, title As String, handler As EventHandler)
        Dim b As New Button With {.Text = title, .AutoSize = True}
        AddHandler b.Click, handler
        panel.Controls.Add(b)
    End Sub

    Private Sub AddField(panel As TableLayoutPanel, title As String, tb As TextBox, row As Integer, ro As Boolean)
        panel.Controls.Add(New Label With {.Text = title, .Dock = DockStyle.Fill}, 0, row)
        tb.Dock = DockStyle.Fill
        tb.ReadOnly = ro
        panel.Controls.Add(tb, 1, row)
    End Sub

    Private Function Connexion() As SqliteConnection
        Return New SqliteConnection("Data Source=" & dbPath)
    End Function

    Private Sub BackupStarterData()
        Dim backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C-Menu-Sauvegardes")
        Directory.CreateDirectory(backupFolder)
        Dim target = Path.Combine(backupFolder, "StarterApplis-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".db")
        Using source = Connexion()
            source.Open()
            Using backup As New SqliteConnection("Data Source=" & target)
                backup.Open()
                source.BackupDatabase(backup)
            End Using
        End Using
    End Sub

    Private Sub InitDb()
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath))
        Using cn = Connexion()
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS StarterApplis (Id INTEGER PRIMARY KEY AUTOINCREMENT, Saisiele TEXT NOT NULL, Modifierle TEXT, Sauvegarde TEXT, Libelle TEXT NOT NULL, Application TEXT NOT NULL, Chemin TEXT NOT NULL)"
                cmd.ExecuteNonQuery()
                cmd.CommandText = "PRAGMA table_info(StarterApplis)"
                Dim hasAction As Boolean = False
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        If String.Equals(reader.GetString(1), "Action", StringComparison.OrdinalIgnoreCase) Then hasAction = True
                    End While
                End Using
                If Not hasAction Then
                    cmd.CommandText = "ALTER TABLE StarterApplis ADD COLUMN Action TEXT NOT NULL DEFAULT ''"
                    cmd.ExecuteNonQuery()
                End If
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('StarterApplis') WHERE name='Priorite'"
                If Convert.ToInt32(cmd.ExecuteScalar()) = 0 Then
                    cmd.CommandText = "ALTER TABLE StarterApplis ADD COLUMN Priorite INTEGER NOT NULL DEFAULT 100"
                    cmd.ExecuteNonQuery()
                End If
            End Using
        End Using
    End Sub

    Private Sub Charger()
        Using cn = Connexion()
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT Id, Saisiele, Modifierle, Sauvegarde, Libelle AS [Libellé], Application, Action, Chemin AS Lien, Priorite AS [Priorité] FROM StarterApplis ORDER BY Priorite ASC, Id ASC"
                Using reader = cmd.ExecuteReader()
                    Dim table As New DataTable()
                    table.Load(reader)
                    grille.DataSource = table
                End Using
            End Using
        End Using
        If grille.Columns.Contains("Id") Then grille.Columns("Id").Visible = False
    End Sub

    Private Sub SelectionChanged(sender As Object, e As EventArgs)
        If grille.CurrentRow Is Nothing OrElse grille.CurrentRow.DataBoundItem Is Nothing Then Return
        Dim row = DirectCast(grille.CurrentRow.DataBoundItem, DataRowView)
        selectedId = CLng(row("Id"))
        saisiele.Text = Convert.ToString(row("Saisiele"))
        modifierle.Text = Convert.ToString(row("Modifierle"))
        sauvegarde.Text = Convert.ToString(row("Sauvegarde"))
        libelle.Text = Convert.ToString(row("Libellé"))
        applicationNom.Text = Convert.ToString(row("Application"))
        chemin.Text = Convert.ToString(row("Lien"))
        actionNom.Text = Convert.ToString(row("Action"))
    End Sub

    Private Sub Nouveau(sender As Object, e As EventArgs)
        grille.ClearSelection()
        grille.CurrentCell = Nothing
        selectedId = 0
        ' Prévisualisation de la date de saisie ; l'heure définitive est fixée à l'enregistrement.
        saisiele.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
        modifierle.Text = ""
        sauvegarde.Text = ""
        libelle.Text = ""
        applicationNom.Text = ""
        chemin.Text = ""
        actionNom.Text = ""
        priorite.Value = 100
        libelle.Focus()
    End Sub

    Private Function Valide() As Boolean
        If String.IsNullOrWhiteSpace(libelle.Text) OrElse String.IsNullOrWhiteSpace(applicationNom.Text) OrElse String.IsNullOrWhiteSpace(chemin.Text) Then
            MessageBox.Show(Me, "Libellé, Application et Chemin sont obligatoires.")
            Return False
        End If
        Return True
    End Function

    Private Sub Ajouter(sender As Object, e As EventArgs)
        If Not Valide() Then Return
        Try
            BackupStarterData()
            Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "INSERT INTO StarterApplis (Saisiele, Modifierle, Sauvegarde, Libelle, Application, Action, Chemin, Priorite) VALUES ($s, '', $b, $l, $a, $act, $c, $p)"
                    cmd.Parameters.AddWithValue("$s", nowText)
                    cmd.Parameters.AddWithValue("$b", nowText)
                    cmd.Parameters.AddWithValue("$l", libelle.Text.Trim())
                    cmd.Parameters.AddWithValue("$a", applicationNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$act", actionNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$c", chemin.Text.Trim())
                    cmd.Parameters.AddWithValue("$p", Convert.ToInt32(priorite.Value))
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Modifier(sender As Object, e As EventArgs)
        If selectedId = 0 OrElse Not Valide() Then Return
        Try
            BackupStarterData()
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "UPDATE StarterApplis SET Modifierle=$m, Sauvegarde=$m, Libelle=$l, Application=$a, Action=$act, Chemin=$c, Priorite=$p WHERE Id=$id"
                    cmd.Parameters.AddWithValue("$m", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    cmd.Parameters.AddWithValue("$l", libelle.Text.Trim())
                    cmd.Parameters.AddWithValue("$a", applicationNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$act", actionNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$c", chemin.Text.Trim())
                    cmd.Parameters.AddWithValue("$p", Convert.ToInt32(priorite.Value))
                    cmd.Parameters.AddWithValue("$id", selectedId)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Demarrer(sender As Object, e As EventArgs)
        DemarrerApplications(Me)
    End Sub

    Public Shared Sub DemarrerApplications(owner As IWin32Window)
        Dim dataFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "CMenu.db")
        If Not File.Exists(dataFile) Then
            MessageBox.Show(owner, "La base de données Started est introuvable.")
            Return
        End If
        Try
            Using cn As New SqliteConnection("Data Source=" & dataFile & ";Mode=ReadOnly")
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "SELECT Application, Chemin FROM StarterApplis ORDER BY Priorite ASC, Id ASC"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim target = reader.GetString(1)
                            Try
                                System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(target) With {.UseShellExecute = True})
                            Catch ex As Exception
                                MessageBox.Show(owner, "Ouverture impossible : " & reader.GetString(0) & Environment.NewLine & ex.Message)
                            End Try
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show(owner, "Démarrage Started impossible : " & ex.Message)
        End Try
    End Sub

    Private Sub Sauvegarder(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            Ajouter(sender, e)
        Else
            Modifier(sender, e)
        End If
    End Sub

    Private Sub Supprimer(sender As Object, e As EventArgs)
        If selectedId = 0 Then Return
        If String.Equals(libelle.Text.Trim(), "Starter-Applis", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(applicationNom.Text.Trim(), "Starter-Applis", StringComparison.OrdinalIgnoreCase) Then
            MessageBox.Show(Me, "Starter-Applis est protégé contre la suppression.")
            Return
        End If
        If MessageBox.Show(Me, "Supprimer cet enregistrement ?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Try
            BackupStarterData()
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "DELETE FROM StarterApplis WHERE Id=$id"
                    cmd.Parameters.AddWithValue("$id", selectedId)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            selectedId = 0
            Charger()
            Nouveau(sender, e)
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Parcourir(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog With {.Filter = "Applications (*.exe)|*.exe|Tous les fichiers (*.*)|*.*"}
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                chemin.Text = dlg.FileName
                applicationNom.Text = Path.GetFileName(dlg.FileName)
            End If
        End Using
    End Sub
End Class

Imports System.Drawing
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.Data.Sqlite

Public Class Frm_Application
    Inherits Form

    Private infoWindow As Form
    Private infoText As TextBox
    Private ReadOnly menuName As String
    Private ReadOnly header As New Label()
    Private ReadOnly menuBar As New MenuStrip()
    Private ReadOnly clock As New Timer With {.Interval = 1000}

    Public Sub New(frmMenuValue As String)
        menuName = If(frmMenuValue, "").Trim()
        If menuName = "" Then Throw New ArgumentException("Frm_Menu est obligatoire.")
        Text = "C-Menu — " & menuName
        StartPosition = FormStartPosition.CenterParent
        ' Hauteur de la fenêtre : 4 cm, calculée selon la résolution DPI Windows.
        Dim height4cm As Integer
        Using screenGraphics = Graphics.FromHwnd(IntPtr.Zero)
            height4cm = CInt(Math.Round(screenGraphics.DpiY * 4.0 / 2.54))
        End Using
        Size = New Size(1000, height4cm)
        MinimumSize = New Size(180, height4cm)
        Font = New Font("Segoe UI", 10)

        Dim colors = MenuColors.Load()
        BackColor = Color.FromArgb(colors.Entry)

        header.Dock = DockStyle.Top
        header.Height = 65
        header.Padding = New Padding(16, 0, 16, 0)
        header.TextAlign = ContentAlignment.MiddleLeft
        header.Font = New Font("Segoe UI", 13, FontStyle.Bold)
        header.BackColor = Color.FromArgb(colors.Table)
        header.ForeColor = Color.FromArgb(colors.Labels)

        menuBar.Dock = DockStyle.Top
        menuBar.AutoSize = True
        menuBar.BackColor = Color.FromArgb(colors.Toolbar)
        menuBar.ForeColor = Color.FromArgb(colors.Labels)
        menuBar.Padding = New Padding(10, 6, 10, 6)
        menuBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow

        Controls.Add(menuBar)
        Controls.Add(header)
        MainMenuStrip = menuBar

        AddHandler clock.Tick, Sub(sender, e) UpdateHeader()
        AddHandler FormClosed, Sub(sender, e)
            clock.Stop()
            slideshowTimer.Stop()
        End Sub
        AddHandler Shown, Sub(sender, e)
            LoadMenus()
            UpdateHeader()
            clock.Start()
        End Sub
    End Sub

    Private Sub UpdateHeader()
        Dim nowValue = DateTime.Now
        header.Text = menuName & " — Nous sommes le " &
            nowValue.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")) &
            " à " & nowValue.ToString("HH:mm:ss")
    End Sub

    Private Sub LoadMenus()
        menuBar.Items.Clear()
        Dim rows = New MenuRepository().GetAll().Where(
            Function(item) String.Equals(item.Frm_Menu.Trim(), menuName, StringComparison.OrdinalIgnoreCase)
        ).OrderBy(Function(item) NumericOrder(item.NumMenu)).
          ThenBy(Function(item) NumericOrder(item.NumSousMenu)).
          ThenBy(Function(item) item.Id).ToList()

        For Each group In rows.Where(Function(item) Not String.IsNullOrWhiteSpace(item.Menu)).
            GroupBy(Function(item) item.NumMenu & "|" & item.Menu, StringComparer.OrdinalIgnoreCase)

            Dim first = group.First()
            Dim topItem As New ToolStripMenuItem(first.Menu.Trim()) With {
                .AutoSize = True,
                .ForeColor = menuBar.ForeColor
            }
            For Each item In group
                If String.IsNullOrWhiteSpace(item.SousMenu) Then
                    If Not String.IsNullOrWhiteSpace(item.Chemin) Then
                        Dim destination = item.Chemin
                        Dim actionName = item.Action
                        Dim info1 = item.Info01
                        Dim info2 = item.Info02
                        AddHandler topItem.Click, Sub(sender, e)
                            ShowInfo(info1, info2)
                            OpenDestination(destination, actionName)
                        End Sub
                    End If
                    Continue For
                End If
                Dim child As New ToolStripMenuItem(item.SousMenu.Trim()) With {
                    .ToolTipText = If(item.Info01, ""),
                    .Tag = item
                }
                AddHandler child.Click, Sub(sender, e)
                    Dim selected = DirectCast(DirectCast(sender, ToolStripMenuItem).Tag, MenuItem)
                    If String.Equals(selected.SousMenu.Trim(), "Anniversaire", StringComparison.OrdinalIgnoreCase) Then
                        Using dialog As New FrmAnniversaire()
                            dialog.ShowDialog(Me)
                        End Using
                    ElseIf IsSlideshow(selected.SousMenu, selected.Action) Then
                        StartSlideshow()
                    Else
                        ShowInfo(selected.Info01, selected.Info02)
                        OpenDestination(selected.Chemin, selected.Action)
                    End If
                End Sub
                topItem.DropDownItems.Add(child)
            Next
            menuBar.Items.Add(topItem)
        Next

        If menuBar.Items.Count = 0 Then
            menuBar.Items.Add(New ToolStripLabel("Aucun menu trouvé pour " & menuName))
        End If
        AdjustMenuWidth()
    End Sub

    Private Shared Function NumericOrder(value As String) As Long
        Dim parsed As Long
        If Long.TryParse(value, parsed) Then Return parsed
        Return Long.MaxValue
    End Function

    Private ReadOnly slideshowTimer As New Timer With {.Interval = 8000}
    Private slideshowPanel As Panel
    Private slideshowPicture As PictureBox
    Private slideshowFiles As String() = Array.Empty(Of String)()
    Private slideshowIndex As Integer = 0
    Private ReadOnly slideshowRandom As New Random()
    Private originalWindowSize As Size
    Private Const SlideshowFolder As String = "C:\Menu\applications\Diaporama"

    Private Sub AdjustMenuWidth()
        ' La largeur suit le libellé du formulaire ou la somme des menus visibles.
        Dim labelText As String = menuName
        Dim labelFont As Font = header.Font
        Dim labelWidth = TextRenderer.MeasureText(labelText, labelFont).Width + 40
        Dim menusWidth As Integer = menuBar.Padding.Horizontal + 16
        For Each entry As ToolStripItem In menuBar.Items
            menusWidth += TextRenderer.MeasureText(entry.Text, menuBar.Font).Width + 30
        Next
        Dim contentWidth = Math.Max(labelWidth, menusWidth)
        If slideshowPanel IsNot Nothing Then contentWidth += slideshowPanel.Width + 12
        Dim frameWidth = Width - ClientSize.Width
        Dim targetWidth = contentWidth + frameWidth
        Dim availableWidth = Screen.FromControl(Me).WorkingArea.Width
        targetWidth = Math.Min(targetWidth, availableWidth)
        MinimumSize = New Size(Math.Min(targetWidth, availableWidth), MinimumSize.Height)
        Width = targetWidth
    End Sub

    Private Sub StartSlideshow()
        Try
            Directory.CreateDirectory(SlideshowFolder)
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
            If slideshowPanel Is Nothing Then
                originalWindowSize = Size
                Dim pxWidth As Integer
                Dim pxHeight As Integer
                Using graphics = CreateGraphics()
                    pxWidth = CInt(Math.Round(graphics.DpiX * 6.22 / 2.54))
                    pxHeight = CInt(Math.Round(graphics.DpiY * 6.19 / 2.54))
                End Using
                slideshowPanel = New Panel With {
                    .Size = New Size(pxWidth, pxHeight),
                    .Dock = DockStyle.Left,
                    .BorderStyle = BorderStyle.FixedSingle,
                    .BackColor = Color.FromArgb(0, 153, 255)
                }
                slideshowPicture = New PictureBox With {
                    .Dock = DockStyle.Fill,
                    .SizeMode = PictureBoxSizeMode.Zoom,
                    .BackColor = Color.FromArgb(0, 153, 255)
                }
                slideshowPanel.Controls.Add(slideshowPicture)
                Controls.Add(slideshowPanel)
                slideshowPanel.BringToFront()
                AdjustMenuWidth()
                Height = Math.Max(Height, pxHeight + 70)
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
            If slideshowFiles.Length = 0 Then
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
                        Dim replacement As New Bitmap(sourceImage)
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

    Private Sub OpenDestination(target As String, actionName As String)
        If String.IsNullOrWhiteSpace(target) Then
            MessageBox.Show("Aucun chemin n'est renseigné pour ce sous-menu.", "C-Menu",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Dim destination = Environment.ExpandEnvironmentVariables(target.Trim())
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
            MessageBox.Show("Impossible d'ouvrir : " & target & Environment.NewLine & ex.Message,
                            "C-Menu", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub
End Class


Public Class FrmAnniversaire
    Inherits Form

    Private ReadOnly databaseFile As String = Path.Combine("C:\", "Menu", "applications", "Anniversaire", "Anniversaires.db")
    Private ReadOnly nom As New TextBox()
    Private ReadOnly prenom As New TextBox()
    Private ReadOnly naissance As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
    Private ReadOnly fete As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
    Private ReadOnly noter As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .ShowCheckBox = True, .Checked = False}
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
        Dim entry As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 200, .ColumnCount = 2, .RowCount = 5, .Padding = New Padding(12)}
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180))
        entry.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim labels = {"Nom", "Prénom", "Date de naissance", "Date de fête", "Date à noter"}
        Dim inputControls As Control() = {nom, prenom, naissance, fete, noter}
        For i = 0 To labels.Length - 1
            entry.RowStyles.Add(New RowStyle(SizeType.Percent, 20))
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
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Evenements (Id INTEGER PRIMARY KEY AUTOINCREMENT, Nom TEXT NOT NULL, Prenom TEXT NOT NULL, Naissance TEXT, Fete TEXT, ANoter TEXT)"
                cmd.ExecuteNonQuery()
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
                cmd.CommandText = "INSERT INTO Evenements (Nom, Prenom, Naissance, Fete, ANoter) VALUES ($nom, $prenom, $naissance, $fete, $noter)"
                cmd.Parameters.AddWithValue("$nom", nom.Text.Trim())
                cmd.Parameters.AddWithValue("$prenom", prenom.Text.Trim())
                cmd.Parameters.AddWithValue("$naissance", If(naissance.Checked, naissance.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("$fete", If(fete.Checked, fete.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.Parameters.AddWithValue("$noter", If(noter.Checked, noter.Value.ToString("yyyy-MM-dd"), CType(DBNull.Value, Object)))
                cmd.ExecuteNonQuery()
            End Using
        End Using
        nom.Clear()
        prenom.Clear()
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
                cmd.CommandText = "SELECT Nom, Prenom, Naissance, Fete, ANoter FROM Evenements"
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim person = reader.GetString(1) & " " & reader.GetString(0)
                        For i = 2 To 4
                            If reader.IsDBNull(i) Then Continue For
                            Dim dateValue As Date
                            If Date.TryParseExact(reader.GetString(i), "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, dateValue) Then
                                upcoming.Add((NextOccurrence(dateValue), person, If(i = 2, "Anniversaire", If(i = 3, "Fête", "Date à noter"))))
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

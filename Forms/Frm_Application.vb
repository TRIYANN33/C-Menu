Imports System.Drawing
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

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
        MinimumSize = New Size(650, height4cm)
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
        AddHandler FormClosed, Sub(sender, e) clock.Stop()
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
                    ShowInfo(selected.Info01, selected.Info02)
                    OpenDestination(selected.Chemin, selected.Action)
                End Sub
                topItem.DropDownItems.Add(child)
            Next
            menuBar.Items.Add(topItem)
        Next

        If menuBar.Items.Count = 0 Then
            menuBar.Items.Add(New ToolStripLabel("Aucun menu trouvé pour " & menuName))
        End If
    End Sub

    Private Shared Function NumericOrder(value As String) As Long
        Dim parsed As Long
        If Long.TryParse(value, parsed) Then Return parsed
        Return Long.MaxValue
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

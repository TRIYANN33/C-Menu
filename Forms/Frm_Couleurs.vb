Imports System.Drawing
Imports System.Windows.Forms

Public Class Frm_Couleurs
    Inherits Form

    Private ReadOnly working As MenuColors
    Private ReadOnly previews As New Dictionary(Of String, Panel)()
    Public ReadOnly Property SelectedColors As MenuColors
        Get
            Return working
        End Get
    End Property

    Public Sub New(current As MenuColors)
        working = current.Copy()
        Text = "C-Menu — Palette de couleurs"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(540, 365)
        Font = New Font("Segoe UI", 10)
        BackColor = Color.FromArgb(241, 247, 237)

        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 280, .Padding = New Padding(18, 14, 18, 5),
            .ColumnCount = 3, .RowCount = 5
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 43))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 32))
        For i = 0 To 4
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 20))
        Next
        AddColorRow(layout, 0, "Barre de titre", "TitleBar")
        AddColorRow(layout, 1, "Libellés", "Labels")
        AddColorRow(layout, 2, "Menu des boutons", "Toolbar")
        AddColorRow(layout, 3, "Zone de saisie", "Entry")
        AddColorRow(layout, 4, "Tableau", "Table")
        Controls.Add(layout)

        Dim actions As New FlowLayoutPanel With {
            .Dock = DockStyle.Bottom, .Height = 65,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(12, 10, 12, 10)
        }
        Dim apply As New Button With {.Text = "Appliquer et fermer", .Width = 165, .Height = 35}
        Dim cancel As New Button With {.Text = "Annuler", .Width = 95, .Height = 35}
        Dim defaults As New Button With {.Text = "Couleurs par défaut", .Width = 170, .Height = 35}
        AddHandler apply.Click, Sub(sender, e)
            DialogResult = DialogResult.OK
            Close()
        End Sub
        AddHandler cancel.Click, Sub(sender, e)
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
        AddHandler defaults.Click, Sub(sender, e)
            Dim standard As New MenuColors()
            working.TitleBar = standard.TitleBar
            working.Labels = standard.Labels
            working.Toolbar = standard.Toolbar
            working.Entry = standard.Entry
            working.Table = standard.Table
            RefreshPreviews()
        End Sub
        actions.Controls.Add(apply)
        actions.Controls.Add(cancel)
        actions.Controls.Add(defaults)
        Controls.Add(actions)
        AcceptButton = apply
        CancelButton = cancel
    End Sub

    Private Function ColorFor(key As String) As Color
        Select Case key
            Case "TitleBar" : Return Color.FromArgb(working.TitleBar)
            Case "Labels" : Return Color.FromArgb(working.Labels)
            Case "Toolbar" : Return Color.FromArgb(working.Toolbar)
            Case "Entry" : Return Color.FromArgb(working.Entry)
            Case Else : Return Color.FromArgb(working.Table)
        End Select
    End Function

    Private Sub SetColor(key As String, chosen As Color)
        Select Case key
            Case "TitleBar" : working.TitleBar = chosen.ToArgb()
            Case "Labels" : working.Labels = chosen.ToArgb()
            Case "Toolbar" : working.Toolbar = chosen.ToArgb()
            Case "Entry" : working.Entry = chosen.ToArgb()
            Case "Table" : working.Table = chosen.ToArgb()
        End Select
        RefreshPreviews()
    End Sub

    Private Sub RefreshPreviews()
        For Each item In previews
            item.Value.BackColor = ColorFor(item.Key)
        Next
    End Sub

    Private Sub AddColorRow(layout As TableLayoutPanel, rowIndex As Integer, title As String, key As String)
        layout.Controls.Add(New Label With {
            .Text = title, .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, rowIndex)
        Dim preview As New Panel With {
            .Dock = DockStyle.Fill, .Margin = New Padding(10, 8, 12, 8),
            .BorderStyle = BorderStyle.FixedSingle,
            .BackColor = ColorFor(key)
        }
        previews.Add(key, preview)
        layout.Controls.Add(preview, 1, rowIndex)
        Dim choose As New Button With {.Text = "Palette...", .Dock = DockStyle.Fill, .Margin = New Padding(8)}
        AddHandler choose.Click, Sub(sender, e)
            Using palette As New ColorDialog()
                palette.FullOpen = True
                palette.AnyColor = True
                palette.Color = ColorFor(key)
                If palette.ShowDialog(Me) = DialogResult.OK Then
                    SetColor(key, palette.Color)
                End If
            End Using
        End Sub
        layout.Controls.Add(choose, 2, rowIndex)
    End Sub
End Class

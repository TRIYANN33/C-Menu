Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Frm_Tri
    Inherits Form

    Private ReadOnly selectors As New List(Of ComboBox)()
    Private ReadOnly directions As New List(Of ComboBox)()
    Private ReadOnly options As (Key As String, Label As String)() = {
        ("NumMenu", "N° Menu"), ("NumSousMenu", "N° Sous-Menu"),
        ("Frm_Menu", "Frm_Menu"), ("Menu", "Menu"),
        ("SousMenu", "Sous-Menu"), ("Info01", "Info_01"),
        ("Info02", "Info_02"), ("Action", "Action"),
        ("Chemin", "Chemin"), ("SaisieLe", "Saisie Le"),
        ("ModifierLe", "Modifier Le"), ("Id", "N° Saisie")
    }

    Public Property SortFields As List(Of (Field As String, Descending As Boolean))

    Public Sub New(Optional previous As List(Of (Field As String, Descending As Boolean)) = Nothing)
        Text = "C-Menu — Choisir les 3 champs de tri"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(565, 285)
        BackColor = Color.FromArgb(241, 247, 237)
        Font = New Font("Segoe UI", 10)
        SortFields = New List(Of (Field As String, Descending As Boolean))()

        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 205, .Padding = New Padding(16, 12, 16, 6),
            .ColumnCount = 3, .RowCount = 4
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 65))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 35))
        For i = 1 To 3
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
        Next
        layout.Controls.Add(New Label With {.Text = "Priorité", .Dock = DockStyle.Fill}, 0, 0)
        layout.Controls.Add(New Label With {.Text = "Champ à trier", .Dock = DockStyle.Fill}, 1, 0)
        layout.Controls.Add(New Label With {.Text = "Ordre", .Dock = DockStyle.Fill}, 2, 0)
        For i = 0 To 2
            layout.Controls.Add(New Label With {
                .Text = (i + 1).ToString() & "ᵉ tri", .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft
            }, 0, i + 1)
            Dim field As New ComboBox With {.Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.DropDownList}
            field.Items.Add("(Aucun)")
            For Each optionItem In options
                field.Items.Add(optionItem.Label)
            Next
            field.SelectedIndex = 0
            If previous IsNot Nothing AndAlso previous.Count > i Then
                For j = 0 To options.Length - 1
                    If options(j).Key = previous(i).Field Then field.SelectedIndex = j + 1
                Next
            End If
            selectors.Add(field)
            layout.Controls.Add(field, 1, i + 1)

            Dim direction As New ComboBox With {.Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.DropDownList}
            direction.Items.AddRange(New Object() {"A-Z / Croissant", "Z-A / Décroissant"})
            direction.SelectedIndex = If(previous IsNot Nothing AndAlso previous.Count > i AndAlso previous(i).Descending, 1, 0)
            directions.Add(direction)
            layout.Controls.Add(direction, 2, i + 1)
        Next

        Dim actions As New FlowLayoutPanel With {
            .Dock = DockStyle.Bottom, .Height = 62, .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(12, 10, 12, 10)
        }
        Dim applyButton As New Button With {.Text = "Appliquer le tri", .Width = 150, .Height = 34}
        Dim cancelButton As New Button With {.Text = "Annuler", .Width = 110, .Height = 34}
        Dim resetButton As New Button With {.Text = "Effacer le tri", .Width = 125, .Height = 34}
        AddHandler applyButton.Click, AddressOf ApplyClicked
        AddHandler cancelButton.Click, Sub(sender, e)
                                           DialogResult = DialogResult.Cancel
                                           Close()
                                       End Sub
        AddHandler resetButton.Click, Sub(sender, e)
                                          SortFields.Clear()
                                          DialogResult = DialogResult.OK
                                          Close()
                                      End Sub
        actions.Controls.Add(applyButton)
        actions.Controls.Add(cancelButton)
        actions.Controls.Add(resetButton)
        Controls.Add(layout)
        Controls.Add(actions)
        AcceptButton = applyButton
        CancelButton = cancelButton
    End Sub

    Private Sub ApplyClicked(sender As Object, e As EventArgs)
        Dim result As New List(Of (Field As String, Descending As Boolean))()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim gapFound As Boolean = False
        For i = 0 To selectors.Count - 1
            Dim index = selectors(i).SelectedIndex
            If index = 0 Then
                gapFound = True
                Continue For
            End If
            If gapFound Then
                MessageBox.Show("Sélectionnez les champs dans l'ordre, sans laisser de ligne vide.", "Tri")
                Return
            End If
            Dim key = options(index - 1).Key
            If Not seen.Add(key) Then
                MessageBox.Show("Un même champ ne peut pas être choisi deux fois.", "Tri")
                Return
            End If
            result.Add((key, directions(i).SelectedIndex = 1))
        Next
        If result.Count = 0 Then
            MessageBox.Show("Choisissez au moins un champ ou cliquez sur Effacer le tri.", "Tri")
            Return
        End If
        SortFields = result
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class

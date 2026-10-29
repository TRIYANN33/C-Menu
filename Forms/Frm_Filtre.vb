Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Frm_Filtre
    Inherits Form

    Private ReadOnly fieldBoxes As New List(Of ComboBox)()
    Private ReadOnly operatorBoxes As New List(Of ComboBox)()
    Private ReadOnly valueBoxes As New List(Of ComboBox)()
    Private ReadOnly options As (Key As String, Label As String)() = {
        ("Id", "N° Saisie"), ("NumMenu", "N° Menu"), ("NumSousMenu", "N° Sous-Menu"),
        ("Frm_Menu", "Frm_Menu"), ("Menu", "Menu"), ("SousMenu", "Sous-Menu"),
        ("Info01", "Info_01"), ("Info02", "Info_02"),
        ("Application", "Application"), ("Action", "Action"), ("Chemin", "Chemin"),
        ("SaisieLe", "Saisie Le"), ("ModifierLe", "Modifier Le")
    }

    Public Property Filters As List(Of (Field As String, OperatorName As String, Value As String))

    Public Sub New(allRows As List(Of MenuItem), Optional previous As List(Of (Field As String, OperatorName As String, Value As String)) = Nothing)
        Text = "C-Menu — Filtrer sur 3 champs"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(850, 280)
        BackColor = Color.FromArgb(241, 247, 237)
        Font = New Font("Segoe UI", 10)
        Filters = New List(Of (Field As String, OperatorName As String, Value As String))()

        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 205, .Padding = New Padding(14, 12, 14, 5),
            .ColumnCount = 4, .RowCount = 4
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 85))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 32))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38))
        For i = 0 To 3
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, If(i = 0, 35, 48)))
        Next
        For Each heading In New String() {"Critère", "Champ", "Condition", "Valeur"}
            layout.Controls.Add(New Label With {.Text = heading, .Dock = DockStyle.Fill}, layout.Controls.Count, 0)
        Next
        For i = 0 To 2
            layout.Controls.Add(New Label With {
                .Text = (i + 1).ToString() & " (ET)", .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft
            }, 0, i + 1)
            Dim field As New ComboBox With {.Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.DropDownList}
            field.Items.Add("(Aucun)")
            For Each optionItem In options
                field.Items.Add(optionItem.Label)
            Next
            field.SelectedIndex = 0
            Dim condition As New ComboBox With {.Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.DropDownList}
            condition.Items.AddRange(New Object() {"Contient", "Est égal à", "Commence par", "Se termine par", "Est différent de"})
            condition.SelectedIndex = 0
            Dim value As New ComboBox With {
                .Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.Drop,
                .AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                .AutoCompleteSource = AutoCompleteSource.ListItems
            }
            Dim currentIndex = i
            AddHandler field.SelectedIndexChanged, Sub(sender, e)
                Dim previousValue = value.Text
                value.Items.Clear()
                If field.SelectedIndex > 0 Then
                    Dim values As New SortedSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
                    Dim key = options(field.SelectedIndex - 1).Key
                    For Each item In allRows
                        Dim candidate = GetValue(item, key)
                        If Not String.IsNullOrWhiteSpace(candidate) Then values.Add(candidate)
                    Next
                    For Each candidate In values
                        value.Items.Add(candidate)
                    Next
                End If
                value.Text = previousValue
            End Sub
            fieldBoxes.Add(field)
            operatorBoxes.Add(condition)
            valueBoxes.Add(value)
            layout.Controls.Add(field, 1, i + 1)
            layout.Controls.Add(condition, 2, i + 1)
            layout.Controls.Add(value, 3, i + 1)
            If previous IsNot Nothing AndAlso previous.Count > i Then
                For j = 0 To options.Length - 1
                    If options(j).Key = previous(i).Field Then field.SelectedIndex = j + 1
                Next
                Select Case previous(i).OperatorName
                    Case "equals" : condition.SelectedIndex = 1
                    Case "starts" : condition.SelectedIndex = 2
                    Case "ends" : condition.SelectedIndex = 3
                    Case "not_equals" : condition.SelectedIndex = 4
                End Select
                value.Text = previous(i).Value
            End If
        Next
        Dim actions As New FlowLayoutPanel With {
            .Dock = DockStyle.Bottom, .Height = 62, .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(12, 10, 12, 10)
        }
        Dim applyButton As New Button With {.Text = "Appliquer", .Width = 135, .Height = 34}
        Dim cancelButton As New Button With {.Text = "Annuler", .Width = 110, .Height = 34}
        Dim clearButton As New Button With {.Text = "Effacer les filtres", .Width = 160, .Height = 34}
        AddHandler applyButton.Click, AddressOf ApplyClicked
        AddHandler cancelButton.Click, Sub(sender, e)
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
        AddHandler clearButton.Click, Sub(sender, e)
            Filters.Clear()
            DialogResult = DialogResult.OK
            Close()
        End Sub
        actions.Controls.Add(applyButton)
        actions.Controls.Add(cancelButton)
        actions.Controls.Add(clearButton)
        Controls.Add(layout)
        Controls.Add(actions)
        AcceptButton = applyButton
        CancelButton = cancelButton
    End Sub

    Private Shared Function GetValue(item As MenuItem, key As String) As String
        Select Case key
            Case "Id" : Return item.Id.ToString()
            Case "NumMenu" : Return item.NumMenu
            Case "NumSousMenu" : Return item.NumSousMenu
            Case "Frm_Menu" : Return item.Frm_Menu
            Case "Menu" : Return item.Menu
            Case "SousMenu" : Return item.SousMenu
            Case "Info01" : Return item.Info01
            Case "Info02" : Return item.Info02
            Case "Application" : Return item.Application
            Case "Action" : Return item.Action
            Case "Chemin" : Return item.Chemin
            Case "SaisieLe" : Return item.SaisieLe.ToString("dd/MM/yyyy HH:mm")
            Case "ModifierLe" : Return If(item.ModifierLe.HasValue, item.ModifierLe.Value.ToString("dd/MM/yyyy HH:mm"), "")
        End Select
        Return ""
    End Function

    Private Sub ApplyClicked(sender As Object, e As EventArgs)
        Dim result As New List(Of (Field As String, OperatorName As String, Value As String))()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim gap As Boolean = False
        Dim operators = New String() {"contains", "equals", "starts", "ends", "not_equals"}
        For i = 0 To 2
            Dim selected = fieldBoxes(i).SelectedIndex
            If selected = 0 Then
                gap = True
                Continue For
            End If
            If gap Then
                MessageBox.Show("Renseignez les critères dans l'ordre, sans ligne vide.", "Filtre")
                Return
            End If
            Dim key = options(selected - 1).Key
            If Not seen.Add(key) Then
                MessageBox.Show("Chaque champ ne peut être utilisé qu'une fois.", "Filtre")
                Return
            End If
            Dim value = valueBoxes(i).Text.Trim()
            If value = "" Then
                MessageBox.Show("Saisissez une valeur pour chaque critère choisi.", "Filtre")
                Return
            End If
            result.Add((key, operators(operatorBoxes(i).SelectedIndex), value))
        Next
        If result.Count = 0 Then
            MessageBox.Show("Choisissez un critère ou cliquez sur Effacer les filtres.", "Filtre")
            Return
        End If
        Filters = result
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class

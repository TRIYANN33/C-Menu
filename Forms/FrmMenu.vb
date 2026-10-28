Imports System.Drawing
Imports System.Windows.Forms
Imports System.ComponentModel
Imports System.Collections.Generic

Public Class FrmMenu
    Inherits Form

    Private ReadOnly repo As New MenuRepository()
    Private ReadOnly fields As New Dictionary(Of String, TextBox)()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly dateCreated As New TextBox()
    Private ReadOnly dateModified As New TextBox()
    Private ReadOnly idBox As New TextBox()
    Private selectedId As Integer = 0
    Private sortAZ As Boolean = False
    Private filterText As String = ""

    Public Sub New()
        Text = "C-Menu — Gestion des menus"
        Size = New Size(1120, 780)
        MinimumSize = New Size(940, 600)
        StartPosition = FormStartPosition.CenterScreen
        Font = New Font("Segoe UI", 10)
        BackColor = Color.FromArgb(166, 218, 73)
        BuildUI()
        ReloadGrid()
    End Sub

    Private Function MakeButton(title As String, handler As EventHandler) As Button
        Dim b As New Button With {.Text = title, .Width = 126, .Height = 33, .Margin = New Padding(4), .BackColor = Color.FromArgb(202, 220, 250), .FlatStyle = FlatStyle.Flat}
        AddHandler b.Click, handler
        Return b
    End Function

    Private Sub BuildUI()
        Dim bar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(18, 6, 0, 0), .BackColor = Color.FromArgb(216, 223, 233)}
        bar.Controls.Add(MakeButton("Enregistrer", AddressOf SaveClicked))
        bar.Controls.Add(MakeButton("Modifier", AddressOf UpdateClicked))
        bar.Controls.Add(MakeButton("Supprimer", AddressOf DeleteClicked))
        bar.Controls.Add(MakeButton("Filtrer", AddressOf FilterClicked))
        bar.Controls.Add(MakeButton("A-Z", AddressOf SortClicked))

        Dim entry As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 220, .BackColor = Color.FromArgb(28, 172, 74), .Padding = New Padding(18, 10, 18, 12), .ColumnCount = 6, .RowCount = 4}
        For i As Integer = 0 To 5
            entry.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 6))
        Next
        For i As Integer = 0 To 3
            entry.RowStyles.Add(New RowStyle(SizeType.Percent, 25))
        Next
        AddField(entry, "N° Saisie", idBox, 0, 0, True)
        AddField(entry, "Saisie Le", dateCreated, 1, 0, True)
        AddField(entry, "Modifier Le", dateModified, 2, 0, True)
        AddField(entry, "N° Menu", "NumMenu", 0, 1)
        AddField(entry, "N° Sous-Menu", "NumSousMenu", 1, 1)
        AddField(entry, "Menu", "Menu", 2, 1)
        AddField(entry, "Sous-Menu", "SousMenu", 3, 1)
        AddField(entry, "Info_01", "Info01", 4, 1)
        AddField(entry, "Info_02", "Info02", 5, 1)
        AddField(entry, "Action", "Action", 0, 2, 2)
        AddField(entry, "Chemin", "Chemin", 2, 2, 4)

        grid.Dock = DockStyle.Fill
        grid.BackgroundColor = Color.FromArgb(166, 218, 73)
        grid.BorderStyle = BorderStyle.None
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 132, 151)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        grid.EnableHeadersVisualStyles = False
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(230, 245, 200)
        grid.RowTemplate.Height = 32
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged

        Dim bottom As New StatusStrip()
        bottom.Items.Add(New ToolStripStatusLabel("C-Menu | SQLite | Sélectionnez une ligne pour modifier"))
        Controls.Add(grid)
        Controls.Add(entry)
        Controls.Add(bar)
        Controls.Add(bottom)
        ClearForm()
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, caption As String, key As String, col As Integer, row As Integer, Optional span As Integer = 1)
        Dim box As New TextBox With {.Dock = DockStyle.Top}
        fields.Add(key, box)
        AddField(layout, caption, box, col, row, False, span)
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, caption As String, box As TextBox, col As Integer, row As Integer, readOnlyField As Boolean, Optional span As Integer = 1)
        box.ReadOnly = readOnlyField
        Dim panel As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(4)}
        Dim label As New Label With {.Text = caption, .Dock = DockStyle.Top, .Height = 22, .ForeColor = Color.Black}
        panel.Controls.Add(box)
        panel.Controls.Add(label)
        box.Top = 23
        box.Dock = DockStyle.None
        box.Location = New Point(0, 24)
        box.Width = 150
        AddHandler panel.Resize, Sub(sender, e) box.Width = Math.Max(40, panel.ClientSize.Width - 2)
        layout.Controls.Add(panel, col, row)
        If span > 1 Then layout.SetColumnSpan(panel, span)
    End Sub

    Private Sub ReloadGrid()
        RemoveHandler grid.SelectionChanged, AddressOf GridSelectionChanged
        grid.DataSource = New BindingList(Of MenuItem)(repo.GetAll(filterText, sortAZ))
        For Each pair In New Dictionary(Of String, String) From {{"Id", "N° Saisie"}, {"SaisieLe", "Saisie Le"}, {"ModifierLe", "Modifier Le"}, {"NumMenu", "N° Menu"}, {"NumSousMenu", "N° Sous-Menu"}, {"Menu", "Menu"}, {"SousMenu", "Sous-Menu"}, {"Info01", "Info_01"}, {"Info02", "Info_02"}, {"Action", "Action"}, {"Chemin", "Chemin"}}
            If grid.Columns.Contains(pair.Key) Then grid.Columns(pair.Key).HeaderText = pair.Value
        Next
        grid.ClearSelection()
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged
    End Sub

    Private Sub ClearForm()
        selectedId = 0
        idBox.Clear()
        dateCreated.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        dateModified.Clear()
        For Each box In fields.Values
            box.Clear()
        Next
    End Sub

    Private Function CurrentItem() As MenuItem
        Return New MenuItem With {.Id = selectedId, .NumMenu = fields("NumMenu").Text.Trim(), .NumSousMenu = fields("NumSousMenu").Text.Trim(), .Menu = fields("Menu").Text.Trim(), .SousMenu = fields("SousMenu").Text.Trim(), .Info01 = fields("Info01").Text.Trim(), .Info02 = fields("Info02").Text.Trim(), .Action = fields("Action").Text.Trim(), .Chemin = fields("Chemin").Text.Trim()}
    End Function

    Private Function ValidateItem(item As MenuItem) As Boolean
        If String.IsNullOrWhiteSpace(item.NumMenu) OrElse String.IsNullOrWhiteSpace(item.Menu) Then
            MessageBox.Show("N° Menu et Menu sont obligatoires.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If
        Return True
    End Function

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        Dim item = CurrentItem()
        If Not ValidateItem(item) Then Return
        repo.Insert(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub UpdateClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez une ligne du tableau.")
            Return
        End If
        Dim item = CurrentItem()
        If Not ValidateItem(item) Then Return
        repo.Update(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub DeleteClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez une ligne du tableau.")
            Return
        End If
        If MessageBox.Show("Supprimer cet enregistrement ?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            repo.Delete(selectedId)
            ClearForm()
            ReloadGrid()
        End If
    End Sub

    Private Sub FilterClicked(sender As Object, e As EventArgs)
        Dim search = Microsoft.VisualBasic.Interaction.InputBox("Rechercher un menu ou sous-menu (vide = tous) :", "Filtrer", filterText)
        filterText = search.Trim()
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub SortClicked(sender As Object, e As EventArgs)
        sortAZ = Not sortAZ
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub GridSelectionChanged(sender As Object, e As EventArgs)
        If grid.SelectedRows.Count = 0 Then Return
        Dim item = TryCast(grid.SelectedRows(0).DataBoundItem, MenuItem)
        If item Is Nothing Then Return
        selectedId = item.Id
        idBox.Text = item.Id.ToString()
        dateCreated.Text = item.SaisieLe.ToString("dd/MM/yyyy HH:mm")
        dateModified.Text = If(item.ModifierLe.HasValue, item.ModifierLe.Value.ToString("dd/MM/yyyy HH:mm"), "")
        fields("NumMenu").Text = item.NumMenu
        fields("NumSousMenu").Text = item.NumSousMenu
        fields("Menu").Text = item.Menu
        fields("SousMenu").Text = item.SousMenu
        fields("Info01").Text = item.Info01
        fields("Info02").Text = item.Info02
        fields("Action").Text = item.Action
        fields("Chemin").Text = item.Chemin
    End Sub
End Class

Imports System.Drawing
Imports System.Windows.Forms
Imports System.ComponentModel
Imports System.Collections.Generic
Imports System.Linq

Public Class FrmMenu
    Inherits Form

    Private ReadOnly repo As New MenuRepository()
    Private ReadOnly fields As New Dictionary(Of String, Control)()
    Private ReadOnly comboKeys As New HashSet(Of String) From {"Frm_Menu", "Menu", "SousMenu", "Info01", "Info02", "Application", "Action"}
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly idBox As New TextBox()
    Private ReadOnly dateCreated As New TextBox()
    Private ReadOnly dateModified As New TextBox()
    Private ReadOnly searchBox As New TextBox()
    Private ReadOnly countLabel As New ToolStripStatusLabel()
    Private selectedId As Integer
    Private sortFields As New List(Of (Field As String, Descending As Boolean))()
    Private activeFilters As New List(Of (Field As String, OperatorName As String, Value As String))()

    Public Sub New()
        Text = "C-Menu | Gestion des menus"
        Size = New Size(1160, 770)
        MinimumSize = New Size(960, 620)
        StartPosition = FormStartPosition.CenterScreen
        Font = New Font("Segoe UI", 10.0F)
        BackColor = Color.FromArgb(241, 247, 237)
        BuildUI()
        ClearForm()
        ReloadGrid()
    End Sub

    Private Function ButtonFor(title As String, handler As EventHandler) As Button
        Dim b As New Button With {
            .Text = title, .Size = New Size(130, 36),
            .Margin = New Padding(0, 0, 10, 0),
            .BackColor = Color.FromArgb(223, 235, 253),
            .ForeColor = Color.FromArgb(25, 55, 83),
            .FlatStyle = FlatStyle.Flat, .Cursor = Cursors.Hand
        }
        b.FlatAppearance.BorderColor = Color.FromArgb(167, 193, 225)
        AddHandler b.Click, handler
        Return b
    End Function

    Private Sub BuildUI()
        Dim toolbar As New FlowLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 58, .Padding = New Padding(16, 10, 0, 8),
            .BackColor = Color.FromArgb(226, 234, 240)
        }
        toolbar.Controls.Add(ButtonFor("Nouveau", AddressOf NewClicked))
        toolbar.Controls.Add(ButtonFor("Enregistrer", AddressOf SaveClicked))
        toolbar.Controls.Add(ButtonFor("Modifier", AddressOf UpdateClicked))
        toolbar.Controls.Add(ButtonFor("Supprimer", AddressOf DeleteClicked))
        toolbar.Controls.Add(ButtonFor("Filtrer", AddressOf FilterClicked))
        toolbar.Controls.Add(ButtonFor("A-Z", AddressOf SortClicked))

        Dim editor As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 224, .ColumnCount = 6, .RowCount = 3,
            .BackColor = Color.FromArgb(29, 171, 79),
            .Padding = New Padding(14, 12, 14, 12)
        }
        For i = 1 To 6
            editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 6))
        Next
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 33.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 33.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 34.0F))
        AddField(editor, "N° Saisie", idBox, 0, 0, 1, True)
        AddField(editor, "Saisie Le", dateCreated, 1, 0, 1, True)
        AddField(editor, "Modifier Le", dateModified, 2, 0, 1, True)
        AddField(editor, "N° Menu", "NumMenu", 3, 0)
        AddField(editor, "N° Sous-Menu", "NumSousMenu", 4, 0)
        AddField(editor, "Frm_Menu", "Frm_Menu", 5, 0)
        AddField(editor, "Menu", "Menu", 0, 1)
        AddField(editor, "Sous-Menu", "SousMenu", 1, 1)
        AddField(editor, "Info_01", "Info01", 2, 1, 2)
        AddField(editor, "Info_02", "Info02", 4, 1, 2)
        AddField(editor, "Application", "Application", 0, 2, 2)
        AddField(editor, "Action", "Action", 2, 2, 2)
        AddField(editor, "Chemin", "Chemin", 4, 2, 2)

        Dim searchPanel As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 60, .ColumnCount = 2,
            .Padding = New Padding(16, 10, 16, 8),
            .BackColor = Color.FromArgb(241, 247, 237)
        }
        searchPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135))
        searchPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim searchLabel As New Label With {
            .Text = "Recherche :", .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font(Font, FontStyle.Bold)
        }
        searchBox.Dock = DockStyle.Fill
        searchBox.PlaceholderText = "Recherche rapide (Entrée pour appliquer)"
        AddHandler searchBox.KeyDown, Sub(sender, e)
                                          If e.KeyCode = Keys.Enter Then
                                              e.SuppressKeyPress = True
                                              ReloadGrid()
                                          End If
                                      End Sub
        searchPanel.Controls.Add(searchLabel, 0, 0)
        searchPanel.Controls.Add(searchBox, 1, 0)

        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 42
        grid.RowTemplate.Height = 34
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 119, 140)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        grid.ColumnHeadersDefaultCellStyle.Font = New Font(Font, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(35, 48, 57)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(66, 150, 178)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(225, 244, 216)
        grid.GridColor = Color.FromArgb(207, 222, 202)
        For Each spec In New (String, String, Integer)() {
            ("Id", "N° Saisie", 65), ("NumMenu", "N° Menu", 75),
            ("NumSousMenu", "N° Sous-Menu", 90), ("Frm_Menu", "Frm_Menu", 120), ("Menu", "Menu", 130),
            ("SousMenu", "Sous-Menu", 130), ("Info01", "Info_01", 90),
            ("Info02", "Info_02", 90), ("Application", "Application", 120), ("Action", "Action", 130),
            ("Chemin", "Chemin", 170), ("SaisieLe", "Saisie Le", 115),
            ("ModifierLe", "Modifier Le", 115)}
            Dim column As New DataGridViewTextBoxColumn With {
                .DataPropertyName = spec.Item1, .Name = spec.Item1,
                .HeaderText = spec.Item2, .MinimumWidth = spec.Item3,
                .FillWeight = CSng(spec.Item3)
            }
            If spec.Item1 = "SaisieLe" OrElse spec.Item1 = "ModifierLe" Then
                column.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm"
            End If
            grid.Columns.Add(column)
        Next
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged

        Dim gridHost As New Panel With {
            .Dock = DockStyle.Fill, .Padding = New Padding(16, 0, 16, 10),
            .BackColor = Color.FromArgb(241, 247, 237)
        }
        gridHost.Controls.Add(grid)
        Dim status As New StatusStrip()
        status.Items.Add(countLabel)
        status.Items.Add(New ToolStripStatusLabel(" | SQLite • Données locales"))
        Controls.Add(gridHost)
        Controls.Add(searchPanel)
        Controls.Add(editor)
        Controls.Add(toolbar)
        Controls.Add(status)
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, title As String, key As String, col As Integer, row As Integer, Optional span As Integer = 1)
        Dim field As Control
        If comboKeys.Contains(key) Then
            field = New ComboBox With {
                .DropDownStyle = ComboBoxStyle.DropDown,
                .AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                .AutoCompleteSource = AutoCompleteSource.ListItems,
                .IntegralHeight = False, .DropDownHeight = 240
            }
        Else
            field = New TextBox()
        End If
        If key = "NumMenu" OrElse key = "NumSousMenu" Then
            Dim numericBox = DirectCast(field, TextBox)
            AddHandler numericBox.KeyPress, Sub(sender, e)
                If Not Char.IsControl(e.KeyChar) AndAlso Not Char.IsDigit(e.KeyChar) Then
                    e.Handled = True
                End If
            End Sub
        End If
        fields.Add(key, field)
        AddField(layout, title, field, col, row, span, False)
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, title As String, tb As Control, col As Integer, row As Integer, span As Integer, readOnlyField As Boolean)
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2,
            .Margin = New Padding(5, 2, 5, 4)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        panel.Controls.Add(New Label With {
            .Text = title, .Dock = DockStyle.Fill,
            .ForeColor = Color.Black, .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        tb.Dock = DockStyle.Top
        If TypeOf tb Is TextBox Then DirectCast(tb, TextBox).ReadOnly = readOnlyField
        tb.BackColor = If(readOnlyField, Color.FromArgb(232, 237, 232), Color.White)
        panel.Controls.Add(tb, 0, 1)
        layout.Controls.Add(panel, col, row)
        If span > 1 Then layout.SetColumnSpan(panel, span)
    End Sub

    Private Sub ReloadGrid()
        RemoveHandler grid.SelectionChanged, AddressOf GridSelectionChanged
        Dim data = repo.GetAll(searchBox.Text.Trim(), sortFields, activeFilters)
        grid.DataSource = New BindingList(Of MenuItem)(data)
        RefreshCombos(data)
        countLabel.Text = data.Count.ToString() & " enregistrement(s)"
        grid.ClearSelection()
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged
    End Sub

    Private Sub RefreshCombos(data As List(Of MenuItem))
        For Each key In comboKeys
            Dim combo = DirectCast(fields(key), ComboBox)
            Dim previous = combo.Text
            Dim values As New SortedSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
            For Each item In data
                Dim value As String = ""
                Select Case key
                    Case "Frm_Menu" : value = item.Frm_Menu
                    Case "Menu" : value = item.Menu
                    Case "SousMenu" : value = item.SousMenu
                    Case "Info01" : value = item.Info01
                    Case "Info02" : value = item.Info02
                    Case "Application" : value = item.Application
                    Case "Action" : value = item.Action
                End Select
                If Not String.IsNullOrWhiteSpace(value) Then values.Add(value.Trim())
            Next
            combo.BeginUpdate()
            combo.Items.Clear()
            combo.Items.AddRange(values.Cast(Of Object)().ToArray())
            combo.EndUpdate()
            combo.Text = previous
        Next
    End Sub

    Private Sub ClearForm()
        selectedId = 0
        idBox.Clear()
        dateCreated.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        dateModified.Clear()
        For Each tb In fields.Values
            tb.Text = ""
        Next
    End Sub

    Private Function CurrentItem() As MenuItem
        Return New MenuItem With {
            .Id = selectedId, .NumMenu = fields("NumMenu").Text.Trim(),
            .NumSousMenu = fields("NumSousMenu").Text.Trim(),
            .Frm_Menu = fields("Frm_Menu").Text.Trim(),
            .Menu = fields("Menu").Text.Trim(),
            .SousMenu = fields("SousMenu").Text.Trim(),
            .Info01 = fields("Info01").Text.Trim(),
            .Info02 = fields("Info02").Text.Trim(),
            .Application = fields("Application").Text.Trim(),
            .Action = fields("Action").Text.Trim(),
            .Chemin = fields("Chemin").Text.Trim()
        }
    End Function

    Private Function IsValid(item As MenuItem) As Boolean
        If item.NumMenu = "" OrElse item.Menu = "" Then
            MessageBox.Show("Les champs N° Menu et Menu sont obligatoires.", "Validation")
            Return False
        End If
        Dim number As Long
        If Not Long.TryParse(item.NumMenu, number) OrElse number < 0 Then
            MessageBox.Show("N° Menu doit être un nombre entier positif ou zéro.", "Validation")
            fields("NumMenu").Focus()
            Return False
        End If
        If item.NumSousMenu <> "" AndAlso (Not Long.TryParse(item.NumSousMenu, number) OrElse number < 0) Then
            MessageBox.Show("N° Sous-Menu doit être un nombre entier positif ou zéro.", "Validation")
            fields("NumSousMenu").Focus()
            Return False
        End If
        Return True
    End Function

    Private Sub NewClicked(sender As Object, e As EventArgs)
        grid.ClearSelection()
        ClearForm()
        fields("NumMenu").Focus()
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        Dim item = CurrentItem()
        If Not IsValid(item) Then Return
        repo.Insert(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub UpdateClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez d'abord un enregistrement.")
            Return
        End If
        Dim item = CurrentItem()
        If Not IsValid(item) Then Return
        repo.Update(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub DeleteClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez d'abord un enregistrement.")
            Return
        End If
        If MessageBox.Show("Supprimer cet enregistrement ?", "Confirmation",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            repo.Delete(selectedId)
            ClearForm()
            ReloadGrid()
        End If
    End Sub

    Private Sub FilterClicked(sender As Object, e As EventArgs)
        Using dialog As New Frm_Filtre(repo.GetAll(), activeFilters)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                activeFilters = dialog.Filters
                ClearForm()
                ReloadGrid()
            End If
        End Using
    End Sub

    Private Sub SortClicked(sender As Object, e As EventArgs)
        Using dialog As New Frm_Tri(sortFields)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                sortFields = dialog.SortFields
                ClearForm()
                ReloadGrid()
            End If
        End Using
    End Sub

    Private Sub GridSelectionChanged(sender As Object, e As EventArgs)
        If grid.SelectedRows.Count = 0 Then Return
        Dim item = TryCast(grid.SelectedRows(0).DataBoundItem, MenuItem)
        If item Is Nothing Then Return
        selectedId = item.Id
        idBox.Text = item.Id.ToString()
        dateCreated.Text = item.SaisieLe.ToString("dd/MM/yyyy HH:mm")
        dateModified.Text = If(item.ModifierLe.HasValue,
                               item.ModifierLe.GetValueOrDefault().ToString("dd/MM/yyyy HH:mm"), "")
        fields("NumMenu").Text = item.NumMenu
        fields("NumSousMenu").Text = item.NumSousMenu
        fields("Frm_Menu").Text = item.Frm_Menu
        fields("Menu").Text = item.Menu
        fields("SousMenu").Text = item.SousMenu
        fields("Info01").Text = item.Info01
        fields("Info02").Text = item.Info02
        fields("Application").Text = item.Application
        fields("Action").Text = item.Action
        fields("Chemin").Text = item.Chemin
    End Sub
End Class

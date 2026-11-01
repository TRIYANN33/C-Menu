Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Linq
Imports System.Media
Imports System.Text.Json
Imports System.Windows.Forms

Public Class CountdownItem
    Public Property Id As Integer
    Public Property Libelle As String = ""
    Public Property Debut As DateTime
    Public Property Fin As DateTime
    Public Property Musique As String = ""
    Public Property CouleurFond As Integer = Color.LightGray.ToArgb()
    Public Property CouleurProgression As Integer = Color.Teal.ToArgb()
    Public Property Annonce As Boolean
End Class

Public Class CountdownManager
    Implements IDisposable

    Private ReadOnly owner As Form
    Private ReadOnly filePath As String
    Private ReadOnly tick As New Timer With {.Interval = 1000}
    Private ReadOnly panel As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 0,
        .AutoScroll = True, .WrapContents = True, .Padding = New Padding(8)}
    Private items As New List(Of CountdownItem)
    Private started As Boolean
    Private baseHeight As Integer

    Public Sub New(host As Form, menuName As String)
        owner = host
        filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "C-Menu", "Countdown_" & menuName & ".json")
        owner.Controls.Add(panel)
        panel.BringToFront()
        AddHandler tick.Tick, AddressOf OnTick
    End Sub

    Public Sub Start()
        If started Then Return
        started = True
        baseHeight = owner.Height
        Try
            If File.Exists(filePath) Then
                items = JsonSerializer.Deserialize(Of List(Of CountdownItem))(File.ReadAllText(filePath))
                If items Is Nothing Then items = New List(Of CountdownItem)
            End If
        Catch ex As Exception
            MessageBox.Show(owner, "Chargement des Countdown timers impossible : " & ex.Message)
        End Try
        Render()
        CheckExpired()
        tick.Start()
    End Sub

    Private Sub Persist()
        Directory.CreateDirectory(Path.GetDirectoryName(filePath))
        File.WriteAllText(filePath, JsonSerializer.Serialize(items))
    End Sub

    Private Sub Render()
        panel.SuspendLayout()
        panel.Controls.Clear()
        Dim width = Math.Max(120, (owner.ClientSize.Width - 38) \ 3)
        For Each item In items.OrderBy(Function(x) x.Id)
            Dim circle As New CountdownCircle With {.Item = item, .Width = width, .Height = 156}
            panel.Controls.Add(circle)
        Next
        Dim rows = CInt(Math.Ceiling(items.Count / 3.0))
        panel.Height = If(rows = 0, 0, Math.Min(500, rows * 162 + 16))
        owner.Height = baseHeight + panel.Height
        panel.ResumeLayout()
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        For Each circle As CountdownCircle In panel.Controls.OfType(Of CountdownCircle)()
            circle.Invalidate()
        Next
        CheckExpired()
    End Sub

    Private Sub CheckExpired()
        Dim expired = items.Where(Function(x) Not x.Annonce AndAlso DateTime.Now >= x.Fin).ToList()
        If expired.Count = 0 Then Return
        For Each item In expired
            item.Annonce = True
        Next
        Try
            Persist()
        Catch ex As Exception
            MessageBox.Show(owner, "Enregistrement des alertes impossible : " & ex.Message)
        End Try
        For Each item In expired
            Try
                If File.Exists(item.Musique) AndAlso String.Equals(Path.GetExtension(item.Musique), ".wav", StringComparison.OrdinalIgnoreCase) Then
                    Using player As New SoundPlayer(item.Musique)
                        player.PlaySync()
                    End Using
                Else
                    SystemSounds.Exclamation.Play()
                End If
            Catch
                SystemSounds.Exclamation.Play()
            End Try
        Next
        MessageBox.Show(owner, "Countdown timer terminé :" & Environment.NewLine &
            String.Join(Environment.NewLine, expired.Select(Function(x) "CT_" & x.Libelle)),
            "Fin des Countdown timers", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Sub OpenEditor()
        Using editor As New Form With {.Text = "Design / Countdown timer", .Size = New Size(740, 590),
            .StartPosition = FormStartPosition.CenterParent}
            Dim list As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
            Dim plus As New Button With {.Text = "+", .Width = 60, .Height = 38, .Dock = DockStyle.Top}
            Dim save As New Button With {.Text = "Enregistrer et fermer", .Height = 42, .Dock = DockStyle.Bottom}
            Dim cards As New List(Of CountdownEditorCard)
            Dim addCard As Action(Of CountdownItem) = Sub(item)
                Dim card As New CountdownEditorCard(item)
                cards.Add(card)
                list.Controls.Add(card)
            End Sub
            For Each item In items.OrderBy(Function(x) x.Id)
                addCard(item)
            Next
            AddHandler plus.Click, Sub()
                Dim nextId = Math.Max(items.Select(Function(x) x.Id).DefaultIfEmpty(0).Max(),
                    cards.Select(Function(x) x.Data.Id).DefaultIfEmpty(0).Max()) + 1
                addCard(New CountdownItem With {.Id = nextId, .Debut = DateTime.Now, .Fin = DateTime.Now.AddMinutes(25)})
            End Sub
            AddHandler save.Click, Sub()
                Try
                    Dim updated As New List(Of CountdownItem)
                    For Each card In cards
                        If card.Deleted Then Continue For
                        updated.Add(card.Collect())
                    Next
                    items = updated
                    Persist()
                    Render()
                    CheckExpired()
                    editor.Close()
                Catch ex As Exception
                    MessageBox.Show(editor, "Enregistrement impossible : " & ex.Message)
                End Try
            End Sub
            editor.Controls.Add(list)
            editor.Controls.Add(save)
            editor.Controls.Add(plus)
            editor.ShowDialog(owner)
        End Using
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        tick.Stop()
        tick.Dispose()
    End Sub
End Class

Public Class CountdownEditorCard
    Inherits Panel
    Public ReadOnly Data As CountdownItem
    Public Property Deleted As Boolean
    Private ReadOnly labelText As New TextBox With {.Width = 200}
    Private ReadOnly mode As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 140}
    Private ReadOnly hours As New NumericUpDown With {.Minimum = 0, .Maximum = 100000, .Width = 75}
    Private ReadOnly minutes As New NumericUpDown With {.Minimum = 0, .Maximum = 100000, .Width = 75}
    Private ReadOnly dateEnd As New DateTimePicker With {.Format = DateTimePickerFormat.Custom, .CustomFormat = "dd/MM/yyyy HH:mm", .Width = 155}
    Private ReadOnly music As New TextBox With {.Width = 260}
    Private ReadOnly bg As New Button With {.Text = "Cercle fond", .Width = 115}
    Private ReadOnly fg As New Button With {.Text = "Cercle restant", .Width = 125}

    Public Sub New(item As CountdownItem)
        Data = item
        Width = 675
        Height = 177
        BorderStyle = BorderStyle.FixedSingle
        Dim title As New Label With {.Text = "CT_" & item.Id.ToString("00"), .Location = New Point(8, 8), .AutoSize = True}
        labelText.Location = New Point(95, 5)
        labelText.Text = item.Libelle
        mode.Items.AddRange(New Object() {"Durée HH / MM", "Date de fin"})
        mode.SelectedIndex = 0
        mode.Location = New Point(8, 40)
        hours.Location = New Point(160, 40)
        minutes.Location = New Point(244, 40)
        dateEnd.Location = New Point(335, 40)
        dateEnd.Value = If(item.Fin > dateEnd.MinDate AndAlso item.Fin < dateEnd.MaxDate, item.Fin, DateTime.Now.AddMinutes(25))
        Dim remaining = item.Fin - DateTime.Now
        hours.Value = CDec(Math.Min(100000, Math.Max(0, Math.Floor(remaining.TotalHours))))
        minutes.Value = CDec(Math.Min(59, Math.Max(0, remaining.Minutes)))
        If item.Debut <> DateTime.MinValue AndAlso item.Fin > item.Debut Then
            mode.SelectedIndex = 1
        End If
        Dim musicLabel As New Label With {.Text = "Musique WAV :", .Location = New Point(8, 80), .AutoSize = True}
        music.Location = New Point(115, 77)
        music.Text = item.Musique
        Dim browse As New Button With {.Text = "Parcourir", .Location = New Point(390, 75), .Width = 95}
        AddHandler browse.Click, Sub()
            Using pick As New OpenFileDialog With {.Filter = "Fichier audio WAV (*.wav)|*.wav"}
                If pick.ShowDialog() = DialogResult.OK Then music.Text = pick.FileName
            End Using
        End Sub
        bg.Location = New Point(8, 115)
        fg.Location = New Point(135, 115)
        bg.BackColor = Color.FromArgb(item.CouleurFond)
        fg.BackColor = Color.FromArgb(item.CouleurProgression)
        AddHandler bg.Click, Sub() ChooseColor(bg)
        AddHandler fg.Click, Sub() ChooseColor(fg)
        Dim remove As New Button With {.Text = "Supprimer", .Location = New Point(280, 115), .Width = 105}
        AddHandler remove.Click, Sub()
            Deleted = True
            Visible = False
        End Sub
        Controls.AddRange(New Control() {title, labelText, mode, hours, minutes, dateEnd,
            musicLabel, music, browse, bg, fg, remove})
    End Sub

    Private Sub ChooseColor(button As Button)
        Using picker As New ColorDialog With {.Color = button.BackColor}
            If picker.ShowDialog() = DialogResult.OK Then button.BackColor = picker.Color
        End Using
    End Sub

    Public Function Collect() As CountdownItem
        Dim name = labelText.Text.Trim()
        If name = "" Then Throw New InvalidOperationException("Saisissez le libellé du Countdown timer CT_" & Data.Id.ToString("00"))
        Dim newEnd As DateTime
        If mode.SelectedIndex = 0 Then
            If hours.Value = 0 AndAlso minutes.Value = 0 Then Throw New InvalidOperationException("Durée nulle pour " & name)
            newEnd = DateTime.Now.AddHours(CDbl(hours.Value)).AddMinutes(CDbl(minutes.Value))
        Else
            newEnd = dateEnd.Value
        End If
        If Data.Libelle <> name OrElse Data.Fin <> newEnd Then Data.Annonce = False
        Data.Libelle = name
        Data.Debut = DateTime.Now
        Data.Fin = newEnd
        Data.Musique = music.Text.Trim()
        Data.CouleurFond = bg.BackColor.ToArgb()
        Data.CouleurProgression = fg.BackColor.ToArgb()
        Return Data
    End Function
End Class

Public Class CountdownCircle
    Inherits Control
    Public Property Item As CountdownItem
    Public Sub New()
        DoubleBuffered = True
        Font = New Font("Segoe UI", 10, FontStyle.Bold)
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If Item Is Nothing Then Return
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim diameter = Math.Min(112, Math.Min(Width - 12, Height - 35))
        Dim rect As New Rectangle((Width - diameter) \ 2, 5, diameter, diameter)
        Using pen As New Pen(Color.FromArgb(Item.CouleurFond), 12)
            e.Graphics.DrawEllipse(pen, rect)
        End Using
        Dim total = Math.Max(1, (Item.Fin - Item.Debut).TotalSeconds)
        Dim remaining = Math.Max(0, (Item.Fin - DateTime.Now).TotalSeconds)
        Using pen As New Pen(Color.FromArgb(Item.CouleurProgression), 9)
            e.Graphics.DrawArc(pen, rect, -90, CSng(360 * Math.Min(1, remaining / total)))
        End Using
        Dim time = TimeSpan.FromSeconds(remaining)
        Dim countdown = If(remaining <= 0, "TERMINÉ", CInt(Math.Floor(time.TotalHours)).ToString("00") & ":" & time.Minutes.ToString("00") & ":" & time.Seconds.ToString("00"))
        TextRenderer.DrawText(e.Graphics, countdown, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        Dim labelRect As New Rectangle(0, diameter + 10, Width, 25)
        TextRenderer.DrawText(e.Graphics, "CT_" & Item.Libelle, Font, labelRect, ForeColor,
            TextFormatFlags.HorizontalCenter Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

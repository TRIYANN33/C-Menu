Imports System
Imports System.IO
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        Try
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            AddHandler Application.Idle, AddressOf AdjustInputLabels
            Application.Run(New FrmMenuAutonome())
        Catch ex As Exception
            Dim message = "Erreur au démarrage de C-Menu :" & Environment.NewLine & ex.ToString()
            Try
                Dim logFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "C-Menu", "Erreur-Demarrage.txt")
                Directory.CreateDirectory(Path.GetDirectoryName(logFile))
                File.WriteAllText(logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") &
                                  Environment.NewLine & message)
            Catch
            End Try
            MessageBox.Show(message, "C-Menu — Erreur de démarrage", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AdjustInputLabels(sender As Object, e As EventArgs)
        For Each window As Form In Application.OpenForms
            If window.IsDisposed Then Continue For
            FitLabels(window)
        Next
    End Sub

    Private Sub FitLabels(parent As Control)
        For Each child As Control In parent.Controls
            Dim label = TryCast(child, Label)
            If label IsNot Nothing AndAlso label.Visible AndAlso Not String.IsNullOrWhiteSpace(label.Text) Then
                ' Adapter uniquement les libellés non ancrés à une zone fixe.
                ' Les libellés Dock=Fill gardent leur mise en page.
                If label.Dock = DockStyle.None AndAlso Not label.AutoSize Then
                    Dim measured = TextRenderer.MeasureText(label.Text, label.Font,
                        New Drawing.Size(Integer.MaxValue, Integer.MaxValue),
                        TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                    Dim requiredWidth = measured.Width + label.Padding.Horizontal + 6
                    If label.Width < requiredWidth Then
                        label.Width = requiredWidth
                    End If
                End If
            End If
            If child.HasChildren Then FitLabels(child)
        Next
    End Sub
End Module

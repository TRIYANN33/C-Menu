Imports System
Imports System.IO
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        Try
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New FrmMenu())
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
End Module

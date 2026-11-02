using System;
using System.IO;

namespace IndicadorGefran.Model
{
    public class AppLogger
    {
        private static AppLogger instance;
        private readonly object writerLock = new object();
        private StreamWriter writer;
        private bool enabled;

        private AppLogger()
        {
        }

        public static AppLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AppLogger();
                }
                return instance;
            }
        }

        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                lock (this.writerLock)
                {
                    if (this.enabled == value)
                        return;
                    if (value)
                    {
                        try
                        {
                            String fileName = "app_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                            String filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                            this.writer = new StreamWriter(filePath, false) { AutoFlush = true };
                        }
                        catch (Exception)
                        {
                            // Falha ao criar o log (permissao, disco cheio) nao pode derrubar o app.
                            return;
                        }
                    }
                    else if (this.writer != null)
                    {
                        this.writer.Close();
                        this.writer = null;
                    }
                    this.enabled = value;
                }
            }
        }

        public void Info(String message)
        {
            WriteLine("INFO ", message);
        }

        public void Warn(String message)
        {
            WriteLine("WARN ", message);
        }

        public void Error(String message, Exception ex = null)
        {
            WriteLine("ERROR", ex == null ? message : message + Environment.NewLine + ex);
        }

        private void WriteLine(String level, String message)
        {
            if (!this.enabled)
                return;
            String line = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message;
            lock (this.writerLock)
            {
                try
                {
                    if (this.writer != null)
                        this.writer.WriteLine(line);
                }
                catch (Exception)
                {
                    // Erro de I/O do proprio logger nao deve propagar.
                }
            }
        }
    }
}

namespace FileTransferService.Models
{
    public class FileTransferRequest
    {
        /* public string SourceServer { get; set; }
         public string SourcePath { get; set; }
         public string DestinationServer { get; set; }
         public string DestinationPath { get; set; }

         public string SourceFtpServerIp { get; set; } // Новый параметр
         public string DestinationFtpServerIp { get; set; } // Новый параметр

         public int SourceFtpPort { get; set; }
         public int DestinationFtpPort { get; set; } */

        public string SourceServer { get; set; }
        public string SourcePath { get; set; }
        public string DestinationServer { get; set; }
        public string DestinationPath { get; set; }
        public string SourceFtpServerIp { get; set; }
        public int SourceFtpPort { get; set; }
        public string DestinationFtpServerIp { get; set; }
        public int DestinationFtpPort { get; set; }
        public IFormFileCollection Files { get; set; }

    }
}
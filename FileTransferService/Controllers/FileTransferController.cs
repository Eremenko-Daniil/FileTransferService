using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FileTransferService.Models;
using FluentFTP;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Drawing;

namespace FileTransferService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileTransferController : ControllerBase
    {
        private readonly ILogger<FileTransferController> _logger;

        public FileTransferController(ILogger<FileTransferController> logger)
        {
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFiles([FromForm] FileTransferRequest request, [FromForm] IFormFileCollection files)
        {
            _logger.LogInformation("Начало загрузки файлов");

            if (files == null || !files.Any())
            {
                _logger.LogWarning("Файлы для загрузки не выбраны");
                return BadRequest("Пожалуйста, выберите файлы для загрузки.");
            }

            if (string.IsNullOrEmpty(request.SourceServer) || string.IsNullOrEmpty(request.DestinationServer) ||
                string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath) ||
                string.IsNullOrEmpty(request.SourceFtpServerIp) || string.IsNullOrEmpty(request.DestinationFtpServerIp) ||
                request.SourceFtpPort == 0 || request.DestinationFtpPort == 0)
            {
                _logger.LogWarning("Недопустимые параметры");
                return BadRequest("Недопустимые параметры.");
            }

            try
            {
                foreach (var file in files)
                {
                    var filePath = Path.Combine(Path.GetTempPath(), file.FileName);
                    _logger.LogInformation("Загрузка файла: {FileName} во временный путь {FilePath}", file.FileName, filePath);

                    try
                    {
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Загружать файл на FTP сервер назначения
                        await Task.Run(() => UploadFileToFtp(request.DestinationFtpServerIp, request.DestinationFtpPort, filePath, file.FileName));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке файла: {FileName}", file.FileName);
                        return StatusCode(500, $"Ошибка при обработке файла: {file.FileName}. Ошибка: {ex.Message}");
                    }
                    finally
                    {
                        // Удалить временный файл
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }
                _logger.LogInformation("Файлы успешно загружены на FTP сервер");
                return Ok("Файлы успешно загружены.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Ошибка сети при подключении к FTP серверу: {Message}", ex.Message);
                return StatusCode(500, $"Ошибка сети при подключении к FTP серверу: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файлов");
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        [HttpGet("checkftp")]
        public IActionResult CheckFtpConnection(int server, string ftpServerIp, int ftpPort)
        {
            try
            {
                using (var ftpClient = new FtpClient(ftpServerIp, ftpPort))
                {
                    // Пробуем подключиться к серверу с анонимной аутентификацией
                    ftpClient.Credentials = new System.Net.NetworkCredential("anonymous", "anonymous");
                    ftpClient.ValidateCertificate += (control, e) => e.Accept = true; // Принимает все сертификаты
                    ftpClient.Connect();
                    ftpClient.Disconnect();
                    return Ok($"Успешное подключение к FTP серверу {server}");
                }
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Ошибка сети при подключении к FTP серверу {Server}: {Message}", server, ex.Message);
                return StatusCode(500, $"Ошибка сети при подключении к FTP серверу {server}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подключении к FTP серверу {Server}: {Message}", server, ex.Message);
                return StatusCode(500, $"Ошибка при подключении к FTP серверу {server}: {ex.Message}");
            }
        }

        [HttpGet("logs")]
        public IActionResult GetLogs()
        {
            var logs = new List<string>();
            string logDirectory = Path.Combine("Logs");

            if (Directory.Exists(logDirectory))
            {
                foreach (var file in Directory.GetFiles(logDirectory))
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            logs.Add(reader.ReadLine());
                        }
                    }
                }
            }

            return Ok(logs);
        }

        [HttpGet("checksum")]
        public IActionResult GetChecksumLogs()
        {
            var logs = new List<string>();
            string logFile = Path.Combine("Logs", "checksum.log");

            if (System.IO.File.Exists(logFile))
            {
                using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        logs.Add(reader.ReadLine());
                    }
                }
            }

            return Ok(logs);
        }

        [HttpPost("clearlogs")]
        public IActionResult ClearLogs()
        {
            string logDirectory = Path.Combine("Logs");

            if (Directory.Exists(logDirectory))
            {
                foreach (var file in Directory.GetFiles(logDirectory))
                {
                    try
                    {
                        using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            // Просто открыть и закрыть файл, чтобы освободить ресурсы
                        }
                        System.IO.File.Delete(file);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Ошибка при удалении файла {File}: {Message}", file, ex.Message);
                    }
                }
            }

            return Ok("Логи успешно очищены.");
        }

        [HttpGet("files")]
        public IActionResult GetFiles(int server, string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
            {
                return BadRequest(new { error = "sourcePath and destinationPath are required." });
            }

            var files = new List<object>();
            string directoryPath = server == 1 ? sourcePath : destinationPath;

            if (Directory.Exists(directoryPath))
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                foreach (var file in directoryInfo.GetFiles())
                {
                    files.Add(new
                    {
                        name = file.Name,
                        size = file.Length,
                        modifiedDate = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }

            return Ok(files);
        }

        private void UploadFileToFtp(string ftpServerIp, int ftpPort, string filePath, string fileName)
        {
            try
            {
                using (var ftpClient = new FtpClient(ftpServerIp, ftpPort))
                {
                    ftpClient.Credentials = new System.Net.NetworkCredential("anonymous", "anonymous");
                    ftpClient.ValidateCertificate += (control, e) => e.Accept = true;
                    ftpClient.Connect(); // Синхронный метод

                    ftpClient.UploadFile(filePath, $"/{fileName}"); // Синхронный метод

                    ftpClient.Disconnect(); // Синхронный метод
                }
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Ошибка сети при подключении к FTP серверу: {Message}", ex.Message);
                throw; // Повторно выбрасываем исключение для обработки в вызывающем методе
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла на FTP сервер: {Message}", ex.Message);
                throw; // Повторно выбрасываем исключение для обработки в вызывающем методе
            }
        }

        [HttpPost]
        public async Task<IActionResult> TransferFiles([FromBody] FileTransferRequest request)
        {
            _logger.LogInformation("Начало передачи файлов");
            if (string.IsNullOrEmpty(request.SourceServer) || string.IsNullOrEmpty(request.DestinationServer) ||
                string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath))
            {
                _logger.LogWarning("Недопустимые параметры");
                return BadRequest("Недопустимые параметры.");
            }

            try
            {
                await TransferFilesAsync(request);
                _logger.LogInformation("Передача файлов успешно завершена");
                return Ok("Передача файлов успешно завершена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при передаче файлов");
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        private async Task TransferFilesAsync(FileTransferRequest request)
        {
            string[] files = System.IO.Directory.GetFiles($@"\\{request.SourceServer}\{request.SourcePath}");

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine($@"\\{request.DestinationServer}\{request.DestinationPath}", fileName);

                try
                {
                    System.IO.File.Copy(file, destFile, overwrite: true);
                    LogTransfer(request.SourcePath, fileName);

                    if (VerifyFileIntegrity(file, destFile))
                    {
                        LogChecksum(request.DestinationPath, fileName, true);
                    }
                    else
                    {
                        LogChecksum(request.DestinationPath, fileName, false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при передаче файла {FileName} из {SourcePath} в {DestinationPath}", fileName, request.SourcePath, request.DestinationPath);
                    LogTransferError(request.SourcePath, fileName, ex.Message);
                }
            }
        }

        private void LogTransfer(string sourcePath, string fileName)
        {
            string logFile = Path.Combine("Logs", "transfer.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            System.IO.File.AppendAllText(logFile, $"{DateTime.Now}: Успешно передан файл {fileName}\n");
        }

        private void LogTransferError(string sourcePath, string fileName, string errorMessage)
        {
            string logFile = Path.Combine("Logs", "transfer_errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            System.IO.File.AppendAllText(logFile, $"{DateTime.Now}: Ошибка при передаче файла {fileName}: {errorMessage}\n");
        }

        private void LogChecksum(string destinationPath, string fileName, bool success)
        {
            string logFile = Path.Combine("Logs", "checksum.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            System.IO.File.AppendAllText(logFile, $"{DateTime.Now}: Проверка целостности файла {fileName} {(success ? "пройдена" : "не пройдена")}\n");
        }

        private bool VerifyFileIntegrity(string sourceFile, string destFile)
        {
            using (var sourceStream = System.IO.File.OpenRead(sourceFile))
            using (var destStream = System.IO.File.OpenRead(destFile))
            using (var md5 = MD5.Create())
            {
                byte[] sourceChecksum = md5.ComputeHash(sourceStream);
                byte[] destChecksum = md5.ComputeHash(destStream);
                return sourceChecksum.SequenceEqual(destChecksum);
            }
        }
    }
}
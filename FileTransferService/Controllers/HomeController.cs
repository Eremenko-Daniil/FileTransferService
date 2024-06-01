using FileTransferService.Models;
using Microsoft.AspNetCore.Mvc;

namespace FileTransferService.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult TransferFiles(FileTransferRequest request)
        {
            if (string.IsNullOrEmpty(request.SourceServer) || string.IsNullOrEmpty(request.DestinationServer) ||
                string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath))
            {
                ModelState.AddModelError("", "Все поля должны быть заполнены.");
                return View("Index");
            }

            // Логика переноса файлов должна быть здесь
            // Используйте сервис или вызовите метод из FileTransferController для выполнения переноса файлов

            ViewBag.Message = "Передача файлов успешно завершена.";
            return View("Index");
        }
    }
}
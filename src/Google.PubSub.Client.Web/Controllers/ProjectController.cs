using System.Collections.Generic;
using Google.PubSub.Client.Web.Configs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Google.PubSub.Client.Web.Controllers
{
    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly PubSubConfig _pubSubConfig;

        public ProjectController(
            ILogger<ProjectController> logger,
            PubSubConfig pubSubConfig)
        {
            _logger = logger;
            _pubSubConfig = pubSubConfig;
        }

        [HttpGet("/")]
        public IActionResult Index()
        {
            return RedirectToAction("List");
        }
        
        [HttpGet("projects")]
        public IActionResult List()
        {
            var projectList = _pubSubConfig.Projects;
            var model = new ProjectListModel {Projects = projectList};

            return View(model);
        }
    }

    public class ProjectListModel
    {
        public IReadOnlyCollection<string> Projects { get; set; }
    }
}
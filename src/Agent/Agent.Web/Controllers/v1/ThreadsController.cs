using Agent.Web.Models.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using System.Text.Json.Serialization;
using System.Text.Json;
using Thread = Agent.Web.Models.v1.Thread;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ThreadsController : ControllerBase
    {
        // In a real implementation, you would inject repositories or services here

        [HttpGet]
        public ActionResult<PagedResponse<Thread>> GetThreads(ODataQueryOptions<Thread> queryOptions)
        {
            // Sample implementation - in a real app, this would query from a database
            var threads = GetSampleThreads();

            // Apply OData filtering and pagination
            return Ok(new PagedResponse<Thread>(threads));
        }

        [HttpGet("{id}")]
        public ActionResult<Thread> GetThread(Guid id)
        {
            var thread = GetSampleThreads().FirstOrDefault(t => t.Id == id);

            if (thread == null)
                return NotFound();

            return Ok(thread);
        }

        [HttpPost]
        public ActionResult<Thread> CreateThread(CreateThreadRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // In a real app, this would save to a database
            var thread = new Thread(
                Id: Guid.NewGuid(),
                Title: GenerateTitle(request.StartMessage.Text),
                StartMessage: new Message(
                    Id: Guid.NewGuid(),
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(
                        Role: Role.User,
                        UserId: "TestUserId",
                        DisplayName: "Test User"),
                    Text: request.StartMessage.Text
                ),
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
            );

            return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, thread);
        }

        [HttpGet("{threadId}/messages")]
        public ActionResult<PagedResponse<Message>> GetMessages(Guid threadId, ODataQueryOptions<Message> queryOptions)
        {
            var thread = GetSampleThreads().FirstOrDefault(t => t.Id == threadId);

            if (thread == null)
                return NotFound();

            // In a real implementation, you would fetch messages from a database
            var messages = GetSampleMessages(threadId);

            return Ok(new PagedResponse<Message>(messages));
        }

        [HttpGet("{threadId}/messages/{messageId}")]
        public ActionResult<Message> GetMessage(Guid threadId, Guid messageId)
        {
            var thread = GetSampleThreads().FirstOrDefault(t => t.Id == threadId);

            if (thread == null)
                return NotFound();

            var message = GetSampleMessages(threadId).FirstOrDefault(m => m.Id == messageId);

            if (message == null)
                return NotFound();

            return Ok(message);
        }

        [HttpPost("{threadId}/messages")]
        public ActionResult<Message> CreateMessage(Guid threadId, CreateMessageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var thread = GetSampleThreads().FirstOrDefault(t => t.Id == threadId);

            if (thread == null)
                return NotFound();

            // In a real app, you would validate and save to a database
            var message = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(
                    Role.User, // Only User role is allowed for API posts
                    "TestUserId", 
                    "Test User"),
                Text: request.Text
            );

            return CreatedAtAction(nameof(GetMessage), new { threadId, messageId = message.Id }, message);
        }

        [HttpGet("{threadId}/actions")]
        public ActionResult<PagedResponse<Models.v1.Action>> GetActions(Guid threadId)
        {
            var thread = GetSampleThreads().FirstOrDefault(t => t.Id == threadId);

            if (thread == null)
                return NotFound();

            // In a real implementation, you would fetch actions from a database
            var actions = GetSampleActions(threadId);

            return Ok(new PagedResponse<Models.v1.Action>(actions));
        }

        // Helper methods to generate sample data - in a real application, these would be database calls

        private static IEnumerable<Thread> GetSampleThreads()
        {
            return
            [
                new Thread(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Title: "Welcome",
                    StartMessage: new Message(
                        Id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                        TimeStamp: DateTime.Parse("2025-03-01"),
                        Author: new Author(
                            Role.SREAgent,
                            "agent-456",
                            "SRE Agent"),
                        Text: "Hello, I am an SRE agent, blah blah blah"
                    ),
                    CreatedTimestamp: DateTime.Parse("2025-03-01"),
                    ModifiedTimestamp: DateTime.Parse("2025-03-11")
                ),
                new Thread(
                    Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Title: "Updating TSL settings",
                    StartMessage: new Message(
                        Id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        TimeStamp: DateTime.Parse("2025-03-01"),
                        Author: new Author(
                            Role.SREAgent,
                            "agent-456",
                            "SRE Agent"),
                        Text: "I have detected the following apps have TLS settings set to an older version. Do you want me to fix that?"
                    ),
                    CreatedTimestamp: DateTime.Parse("2025-03-01"),
                    ModifiedTimestamp: DateTime.Parse("2025-03-11")
                )
            ];
        }

        private static IEnumerable<Message> GetSampleMessages(Guid threadId)
        {
            if (threadId == Guid.Parse("11111111-1111-1111-1111-111111111111"))
            {
                return
                [
                    new Message(
                        Id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                        TimeStamp: DateTime.Parse("2025-03-12"),
                        Author : new Author(
                            Role.User, 
                            "TestUserId", 
                            "Test User"),
                        Text: "Hello, can you tell me which subscriptions I have an access to?"
                    ),
                    new Message(
                        Id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        TimeStamp: DateTime.Parse("2025-03-12"),
                        Author : new Author(
                            Role.SREAgent, 
                            "TestUserId", 
                            "Test User"),
                        Text: "You have access to the following subscriptions ..."
                    )
                ];
            }

            return new List<Message>();
        }

        private static IEnumerable<Models.v1.Action> GetSampleActions(Guid threadId)
        {
            if (threadId == Guid.Parse("22222222-2222-2222-2222-222222222222"))
            {
                return
                [
                    new Models.v1.Action(
                        Id: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                        Title: "Applied TLS configuration change to an app service named myapservice1",
                        TimeStamp: DateTime.Parse("2025-03-10"),
                        Status: ActionStatus.Completed
                    ),
                    new Models.v1.Action(
                        Id: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                        Title: "Applied TLS configuration change to an app service named myapservice2",
                        TimeStamp: DateTime.Parse("2025-03-11"),
                        Status: ActionStatus.Completed
                    )
                ];
            }

            return new List<Models.v1.Action>();
        }

        private static string GenerateTitle(string message)
        {
            // In a real application, we will use LLM to generate a title
            return message.Length <= 50 ? message : message.Substring(0, 47) + "...";
        }
    }
}
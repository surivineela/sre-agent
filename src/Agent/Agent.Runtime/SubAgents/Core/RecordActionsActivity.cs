using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Runtime.SubAgents.Core
{
    public record RecordActionInput(
        Guid ThreadId,
        string Title,
        ActionStatus Status = ActionStatus.Pending);

    [DurableTask]
    public class RecordActionActivity : TaskActivity<RecordActionInput, Action>
    {
        private readonly IRecordActionsPlugin _recordActionsPlugin;

        public RecordActionActivity(IRecordActionsPlugin recordActionsPlugin)
        {
            _recordActionsPlugin = recordActionsPlugin ?? throw new ArgumentNullException(nameof(recordActionsPlugin));
        }

        public override async Task<Action> RunAsync(TaskActivityContext context, RecordActionInput input)
        {
            // Record the action using the plugin
            var action = await _recordActionsPlugin.RecordAction(
                input.ThreadId,
                input.Title,
                input.Status);

            return action;
        }
    }

    public record GetActionDetailsInput(
        Guid ThreadId,
        Guid ActionId);

    [DurableTask]
    public class GetActionDetailsActivity : TaskActivity<GetActionDetailsInput, Action>
    {
        private readonly IRecordActionsPlugin _recordActionsPlugin;

        public GetActionDetailsActivity(IRecordActionsPlugin recordActionsPlugin)
        {
            _recordActionsPlugin = recordActionsPlugin ?? throw new ArgumentNullException(nameof(recordActionsPlugin));
        }

        public override async Task<Action> RunAsync(TaskActivityContext context, GetActionDetailsInput input)
        {
            // Get the action details using the plugin
            var action = await _recordActionsPlugin.GetAction(
                input.ThreadId,
                input.ActionId);

            return action;
        }
    }
}

# Notification System Usage Guide

The Agent Portal includes a comprehensive notification system for tracking operations and displaying user feedback.

## Basic Usage

### Import the Hook

```typescript
import { useNotifications } from '../../Common/Contexts/NotificationContext';

const MyComponent = () => {
    const notifications = useNotifications();
    // ...
};
```

## API Patterns

### 1. Explicit API (Recommended for most cases)

The explicit API is the primary pattern, familiar to the team:

```typescript
// Start a long-running operation
const notifId = notifications.start('Deploying agent...', 'Provisioning resources');

try {
    const result = await deployAgent();
    // Update to success
    notifications.succeed(notifId, 'Deployment complete!', `Agent ${result.name} is running`);
} catch (error) {
    // Update to error
    notifications.fail(notifId, 'Deployment failed', error.message);
}
```

### 2. One-off Notifications

For simple, immediate feedback:

```typescript
// Info notification
notifications.info('Settings saved');

// Warning notification
notifications.warning('Connection unstable', 'Retrying in 5 seconds...');

// Error notification
notifications.error('Failed to load data', 'Please try again');
```

### 3. Promise-based API (Optional)

For modern async/await code:

```typescript
const notifId = notifications.trackPromise(
    'Building container...',
    buildContainerAsync(),
    {
        onSuccess: (result) => ({
            title: 'Build complete!',
            description: `Image: ${result.imageTag}`,
        }),
        onError: (err) => ({
            title: 'Build failed',
            description: err.message,
        }),
    }
);
```

### 4. Polling API (Rare)

For operations tracked via external APIs:

```typescript
const notifId = notifications.startWithPolling('Processing data...', {
    pollFn: async () => {
        const status = await checkJobStatus(jobId);

        if (status.state === 'completed') {
            return {
                complete: true,
                success: true,
                title: 'Processing complete!',
                description: `Processed ${status.itemCount} items`
            };
        }

        if (status.state === 'failed') {
            return {
                complete: true,
                success: false,
                title: 'Processing failed',
                description: status.error
            };
        }

        // Still in progress
        return { complete: false };
    },
    interval: 3000, // Poll every 3 seconds
    maxAttempts: 20, // Give up after 20 attempts (1 minute)
});
```

## Real-World Examples

### Example 1: Agent Creation

```typescript
const handleCreateAgent = async (config: AgentConfig) => {
    const notifId = notifications.start(
        'Creating agent...',
        'Initializing configuration'
    );

    try {
        const agent = await agentService.create(config);
        notifications.succeed(
            notifId,
            'Agent created successfully!',
            `${agent.name} is ready to use`
        );
        navigate(`/agents/${agent.id}`);
    } catch (error) {
        notifications.fail(
            notifId,
            'Failed to create agent',
            error instanceof Error ? error.message : 'Unknown error'
        );
    }
};
```

### Example 2: Batch Operation with Promise

```typescript
const handleBatchDelete = async (itemIds: string[]) => {
    notifications.trackPromise(
        `Deleting ${itemIds.length} items...`,
        Promise.all(itemIds.map(id => deleteItem(id))),
        {
            onSuccess: () => ({
                title: 'Deletion complete',
                description: `Successfully deleted ${itemIds.length} items`,
            }),
            onError: (err) => ({
                title: 'Deletion failed',
                description: `Failed to delete items: ${err.message}`,
            }),
        }
    );
};
```

### Example 3: Scheduled Task with Polling

```typescript
const handleScheduleTask = async (taskConfig: TaskConfig) => {
    const response = await scheduleTask(taskConfig);

    notifications.startWithPolling('Running scheduled task...', {
        pollFn: async () => {
            const status = await getTaskStatus(response.taskId);

            if (status.completed) {
                return {
                    complete: true,
                    success: status.success,
                    title: status.success ? 'Task completed' : 'Task failed',
                    description: status.output,
                };
            }

            return { complete: false };
        },
        interval: 2000,
        maxAttempts: 30,
    });
};
```

## Best Practices

1. **Use explicit API for most operations** - It's clear, predictable, and familiar to the team
2. **Provide descriptive titles** - Users should understand what's happening at a glance
3. **Include context in descriptions** - Add resource names, counts, or other relevant details
4. **Handle errors gracefully** - Always catch errors and update notifications appropriately
5. **Don't spam notifications** - Batch related operations when possible
6. **Use appropriate status** - Choose between info/warning/error/success based on outcome

## UI Behavior

- **Toasts**: All notifications appear as toasts in the top-right corner for 4 seconds
- **Drawer**: Click the bell icon in the navbar to see notification history
- **Badge**: Unread count is shown on the bell icon
- **Spinner**: A spinner appears on the bell icon when operations are in progress
- **Auto-dismiss**: Toasts auto-dismiss, but notifications remain in the drawer until manually dismissed
- **Dismiss options**:
  - Dismiss individual notifications via the X button
  - Dismiss all notifications
  - Dismiss only completed notifications (keeps in-progress)

## TypeScript Types

```typescript
type NotificationStatus = 'in-progress' | 'success' | 'error' | 'warning' | 'info';

interface Notification {
    id: string;
    title: string;
    description?: string;
    status: NotificationStatus;
    timestamp: Date;
}
```

## Advanced: Manual Dismissal

If you need to programmatically dismiss a notification:

```typescript
const notifId = notifications.start('Processing...');

// Later, dismiss it
notifications.dismiss(notifId);

// Or dismiss all
notifications.dismissAll();

// Or dismiss only completed ones
notifications.dismissCompleted();
```

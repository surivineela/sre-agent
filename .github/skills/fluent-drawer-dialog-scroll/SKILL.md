---
name: fluent-drawer-dialog-scroll
description: Patterns for implementing proper scrolling behavior in Fluent UI v9 Drawer and Dialog components. Use when creating Dialog or Drawer components with scrollable content, when content may exceed viewport height, or when implementing multi-section layouts within modals.
---

# Fluent UI Drawer and Dialog Scroll Patterns

This skill provides patterns for implementing proper scrolling behavior in Fluent UI v9 Drawer and Dialog components.

## When to Use

Apply these patterns when:
- Creating Dialog or Drawer components with scrollable content
- Content may exceed the viewport height
- Need to ensure consistent scroll behavior across browsers
- Implementing multi-section layouts within modals

## Core Pattern: Scrollable Dialog/Drawer

The key to proper scrolling is a combination of:
1. **Fixed outer container** with constrained height
2. **Flex layout** to distribute space
3. **Scroll container** with `height: '0px'` and `flex: '1 1 auto'` to enable scrolling
4. **Stable scrollbar gutter** to prevent layout shifts

### Basic Structure

```tsx
// Styles
const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '900px',
        maxHeight: '90vh',
        height: '90vh',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        flex: '1 1 auto',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        position: 'relative',
        overflowY: 'auto',
        flex: '1 1 auto',
        height: '0px',  // Critical: enables flex-based scrolling
    },
});

// Component
export const MyDialog = ({ isOpen, onClose }: Props) => {
    const styles = useStyles();
    
    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>Title</DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        {/* Scrollable content here */}
                    </DialogContent>
                    <DialogActions>
                        <Button onClick={onClose}>Close</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
```

## Pattern: Scrollable List with Stable Gutter

For lists or tables that scroll independently:

```tsx
const useStyles = makeStyles({
    tableContainer: {
        flex: '1',
        display: 'flex',
        flexDirection: 'column',
        minHeight: '0',
        minWidth: '0',
        overflow: 'hidden',
    },
    scrollableList: {
        flex: '1',
        overflowY: 'auto',
        overflowX: 'auto',
        minHeight: '0',
        minWidth: '0',
        scrollbarGutter: 'stable',  // Prevents layout shift when scrollbar appears
    },
});
```

## Pattern: Split-Pane Dialog with Independent Scrolling

For dialogs with multiple scrollable sections (e.g., form on left, preview on right):

```tsx
const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '1200px',
        width: '80vw',
        maxHeight: '800px',
        height: '80vh',
        padding: '0px',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: '0px',
    },
    contentOuterWrapper: {
        position: 'relative',
        display: 'flex',
        flex: '1 1 auto',
        height: '0%',
        flexDirection: 'column',
        overflowY: 'hidden',
    },
    contentInnerWrapper: {
        display: 'flex',
        flex: '1 1 auto',
        height: '0%',
        flexDirection: 'row',
        overflowY: 'hidden',
        '@media (width < 1000px)': {
            flexDirection: 'column',
            overflowY: 'auto',
        },
    },
    scrollablePane: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        flex: '1 1 auto',
        overflowY: 'auto',
        padding: '20px',
        width: '50%',
        '@media (width < 1000px)': {
            overflowY: 'visible',
            width: 'unset',
        },
    },
});
```

## Pattern: Panel/Drawer with Header and Scrollable Body

```tsx
const useStyles = makeStyles({
    panelRoot: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        ...shorthands.overflow('hidden'),
    },
    panelHeader: {
        flexShrink: 0,
        padding: '16px',
    },
    panelBody: {
        flex: '1 1 auto',
        overflowY: 'auto',
        overflowX: 'hidden',
        scrollbarGutter: 'stable',
        padding: '0 16px 16px 16px',
    },
});
```

## Critical Rules

### DO ✅

1. **Use `height: '0px'` with `flex: '1 1 auto'`** for scrollable containers
   - This is the key pattern that enables flex-based scrolling
   
2. **Add `overflow: 'hidden'` to parent containers**
   - Prevents content from expanding beyond bounds
   
3. **Use `scrollbarGutter: 'stable'`** for consistent layouts
   - Prevents content from jumping when scrollbar appears/disappears
   
4. **Set explicit `maxHeight` on DialogSurface**
   - Use viewport-relative units: `maxHeight: '90vh'`
   
5. **Test vertical scrolling and resizing before submitting PR**
   - Resize the window to verify scroll behavior at various sizes

### DON'T ❌

1. **Don't omit `height: '0px'` on scroll containers**
   - Without it, flex children may not scroll properly
   
2. **Don't use `overflow: 'scroll'` (prefer `auto`)**
   - `scroll` always shows scrollbars, `auto` shows them only when needed
   
3. **Don't forget responsive breakpoints**
   - Consider mobile/narrow layouts with `@media` queries

## Common Issues

### Content doesn't scroll
- Ensure parent has `overflow: 'hidden'`
- Ensure scroll container has `height: '0px'` or `height: '0%'`
- Ensure scroll container has `flex: '1 1 auto'`

### Scrollbar causes layout shift
- Add `scrollbarGutter: 'stable'` to the scroll container

### Dialog content overflows viewport
- Add `maxHeight: '90vh'` (or similar) to DialogSurface
- Ensure DialogBody and DialogContent use flex layout

## References

- [Fluent UI Dialog Documentation](https://react.fluentui.dev/?path=/docs/components-dialog--docs)
- [Fluent UI Drawer Documentation](https://react.fluentui.dev/?path=/docs/components-drawer--docs)
- Source examples:
  - [TriggerAgentDrawer.tsx](../../src/Agent/Agent.Web/Client/src/src/Space/IncidentManagement/IncidentsOverview/TriggerAgentDrawer.tsx)
  - [AgentCreateDialog.Styles.tsx](../../src/Agent/Agent.Web/Client/src/src/Space/Graph/AgentCreateDialog/AgentCreateDialog.Styles.tsx)
  - [FeedbackDetailPanel.tsx](../../src/Agent/Agent.Web/Client/src/src/Space/Feedback/FeedbackDetailPanel.tsx)

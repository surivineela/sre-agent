# Reusable Components Reference

This document catalogs reusable UI components available in `src/Common/Components/`.

## Wizard

Multi-step dialog pattern with stepper UI and navigation controls.

**Location:** `src/Common/Components/Wizard/`

**Key Components:**

- `WizardDialog` - Main dialog wrapper with header, stepper, body, and action buttons
- `WizardStepper` - Visual step indicator showing progress and step titles

**Usage Example:**

```typescript
const steps: WizardStep[] = [
    { label: 'Basics', isValid: true },
    { label: 'Configuration', isValid: false },
    { label: 'Review', isValid: true },
];

<WizardDialog
    open={isOpen}
    onOpenChange={setIsOpen}
    title="Create Agent"
    currentStep={currentStep}
    steps={steps}
    onBack={() => setCurrentStep(currentStep - 1)}
    onNext={() => setCurrentStep(currentStep + 1)}
    onSubmit={handleSubmit}
    isSubmitting={isSubmitting}
    canGoBack={currentStep > 0}
    canGoNext={currentStep < steps.length - 1}
>
    {/* Step content here */}
</WizardDialog>
```

**See:** `src/Views/Home/Create/CreateAgentDialog.tsx` for complete implementation

## Resource Selection Components

### ResourceGroupPicker

Multi-select resource group picker with search, filter, and pill-based selection across multiple subscriptions.

**Location:** `src/Common/Components/ResourceGroupPicker/`

**Props:**

- `subscriptionIds: string[]` - Subscriptions to load resource groups from
- `value: string[]` - Selected resource group IDs
- `onChange: (ids: string[]) => void` - Selection change callback
- `placeholder?: string` - Input placeholder text
- `telemetrySource: TelemetrySource` - Telemetry context

**Features:**

- Search across all resource groups
- Filter by subscription using pills
- Shows selection count and "Select All" option
- Skeleton loading state
- Error handling with retry

### SubscriptionDropdown

Single-select dropdown for subscriptions with Formik integration.

**Location:** `src/Common/Components/SubscriptionDropdown.tsx`

**Props:**

- `selectedSubscriptionId: string | undefined` - Currently selected subscription
- `onSubscriptionChange: (id: string) => void` - Selection callback
- `label?: string` - Field label
- `required?: boolean` - Shows required indicator
- `disabled?: boolean` - Disables dropdown
- `telemetrySource: TelemetrySource` - Telemetry context

**Formik Usage:**

```typescript
<Field name="subscriptionId">
    {({ field, form }: FieldProps) => (
        <SubscriptionDropdown
            selectedSubscriptionId={field.value}
            onSubscriptionChange={(id) => form.setFieldValue('subscriptionId', id)}
            required
            telemetrySource={TelemetrySource.MyView}
        />
    )}
</Field>
```

### ResourceGroupDropdown

Single-select dropdown for resource groups within a subscription.

**Location:** `src/Common/Components/ResourceGroupDropdown.tsx`

**Props:**

- `subscriptionId: string` - Parent subscription
- `selectedResourceGroupName: string | undefined` - Selected RG name
- `onResourceGroupChange: (name: string) => void` - Selection callback
- `label?: string` - Field label
- `required?: boolean` - Shows required indicator
- `allowCreateNew?: boolean` - Shows "Create new" option
- `telemetrySource: TelemetrySource` - Telemetry context

## Form Components

### ImageRadioGroup

Radio button group with image icons, used for visual selection (e.g., permission templates).

**Location:** `src/Common/Components/ImageRadioGroup.tsx`

**Type-safe with generics:**

```typescript
interface ImageRadioOption<T extends string> {
    value: T;
    label: string;
    description: string;
    imageSrc: string;
}

<ImageRadioGroup<'minimal' | 'standard' | 'full'>
    options={permissionOptions}
    value={selectedPermission}
    onChange={setSelectedPermission}
    ariaLabel="Select permission level"
    name="permissions"
/>
```

### Formik Components

**Location:** `src/Common/Components/Formik/`

Collection of Formik-compatible form field wrappers (TBD - document as they're created).

## Filter Components

### PillFilter

Filter UI with pill-based selection for categories/tags.

**Location:** `src/Common/Components/PillFilter/`

**Key Components:**

- `Pill` - Individual pill with label, optional count, dismissible
- `ListWithSearch` - Searchable list with "Select All" option

**Usage:**

```typescript
<ListWithSearch
    items={categories}
    selectedItems={selectedCategories}
    onSelectedItemsChange={setSelectedCategories}
    placeholder="Filter categories..."
    allLabel="All categories"
/>
```

## Text Components

### TextWithLink

Inline text with embedded clickable links, preserves text formatting.

**Location:** `src/Common/Components/TextWithLink.tsx`

**Props:**

```typescript
interface ITextWithLinkProps {
    text: string;           // Text with {0}, {1} placeholders
    links: ILink[];         // Array of { text, href, onClick? }
    openInNewTab?: boolean; // Default: true
}
```

**Example:**

```typescript
<TextWithLink
    text="Read the {0} or contact {1} for help"
    links={[
        { text: 'documentation', href: 'https://docs.example.com' },
        { text: 'support', onClick: () => openSupportDialog() }
    ]}
/>
```

### LearnMoreLink

Consistent "Learn more" link styling with external icon.

**Location:** `src/Common/Components/LearnMoreLink.tsx`

**Props:**

- `href: string` - Link destination
- `text?: string` - Link text (default: "Learn more")
- `inline?: boolean` - Inline vs block display

## Best Practices

1. **Pass telemetrySource** - Most components accept `telemetrySource` parameter for tracking
2. **Use Formik integration** - Dropdowns and inputs support Formik `Field` component
3. **Check loading states** - Components show loading skeletons automatically
4. **Handle errors** - Components display error states; provide retry logic where applicable
5. **Leverage type safety** - Use generics (e.g., `ImageRadioGroup<T>`) for type-safe selections
6. **Reuse existing** - Check this catalog before creating new components

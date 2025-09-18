import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi, type MockedFunction } from 'vitest';
import { FilterProps, TimeRangeKeyLabelPair, TimeRangeValue, TimespanKeys } from '../PillFilter/Contracts';
import { PillFilter } from '../PillFilter/PillFilter';

// Mock the Fluent UI initializeIcons function
vi.mock('@fluentui/react', async () => {
    const actual = await vi.importActual('@fluentui/react');
    return {
        ...actual,
        initializeIcons: vi.fn(),
    };
});

const TestWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
    <FluentProvider theme={webLightTheme}>
        <IntlProvider locale="en" messages={{}}>
            {children}
        </IntlProvider>
    </FluentProvider>
);

const defaultOptions: TimeRangeKeyLabelPair[] = [
    { key: TimespanKeys.OneHour, label: '1 Hour' },
    { key: TimespanKeys.SixHours, label: '6 Hours' },
    { key: TimespanKeys.TwelveHours, label: '12 Hours' },
    { key: TimespanKeys.TwentyFourHours, label: '24 Hours' },
];

const defaultSelectedValue: TimeRangeValue = {
    key: TimespanKeys.OneHour,
};

const defaultProps: FilterProps = {
    filterType: 'timeRange',
    label: 'Time Range',
    options: defaultOptions,
    onApply: vi.fn(),
    selectedValue: defaultSelectedValue,
};

describe('TimeRangePillFilter', () => {
    let mockOnApply: MockedFunction<(value: TimeRangeValue) => void>;

    beforeEach(() => {
        mockOnApply = vi.fn();
        vi.clearAllMocks();
    });

    it('renders with basic props', () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        expect(pillButton).toBeInTheDocument();
        expect(within(pillButton).getByText('Time Range')).toBeInTheDocument();
        expect(within(pillButton).getByText('1 Hour')).toBeInTheDocument();
    });

    it('displays custom display value when provided', () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} displayValue="Custom Display" />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        expect(pillButton).toBeInTheDocument();
        expect(within(pillButton).getByText('Custom Display')).toBeInTheDocument();
    });

    it('opens popover when clicked', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        // Wait for the popover to appear
        await waitFor(() => {
            expect(screen.getByText('Apply')).toBeInTheDocument();
        });

        // Check that radio options are displayed
        expect(screen.getByLabelText('1 Hour')).toBeInTheDocument();
        expect(screen.getByLabelText('6 Hours')).toBeInTheDocument();
        expect(screen.getByLabelText('12 Hours')).toBeInTheDocument();
        expect(screen.getByLabelText('24 Hours')).toBeInTheDocument();
        expect(screen.queryByLabelText('Custom')).not.toBeInTheDocument();
    });

    it('shows selected option as checked', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            const oneHourRadio = screen.getByLabelText('1 Hour');
            expect(oneHourRadio).toBeChecked();
        });
    });

    it('calls onApply when Apply button is clicked', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            expect(screen.getByText('Apply')).toBeInTheDocument();
        });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await userEvent.click(applyButton);

        expect(mockOnApply).toHaveBeenCalledWith({
            key: TimespanKeys.OneHour,
            start: undefined,
            end: undefined,
        });
    });

    it('changes selection when different radio option is clicked', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            const sixHoursRadio = screen.getByLabelText('6 Hours');
            expect(sixHoursRadio).toBeInTheDocument();
        });

        const sixHoursRadio = screen.getByLabelText('6 Hours');
        await userEvent.click(sixHoursRadio);

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await userEvent.click(applyButton);

        expect(mockOnApply).toHaveBeenCalledWith({
            key: TimespanKeys.SixHours,
            start: undefined,
            end: undefined,
        });
    });

    it('shows custom option when addCustomOption is enabled', async () => {
        const propsWithCustom = {
            ...defaultProps,
            customTimeRangeProps: {
                addCustomOption: true,
            },
        };

        render(
            <TestWrapper>
                <PillFilter {...propsWithCustom} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            expect(screen.getByLabelText('Custom')).toBeInTheDocument();
        });
    });

    it('shows date and time pickers when custom option is selected', async () => {
        const propsWithCustom = {
            ...defaultProps,
            customTimeRangeProps: {
                addCustomOption: true,
            },
        };

        render(
            <TestWrapper>
                <PillFilter {...propsWithCustom} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            const customRadio = screen.getByLabelText('Custom');
            expect(customRadio).toBeInTheDocument();
        });

        const customRadio = screen.getByLabelText('Custom');
        await userEvent.click(customRadio);

        await waitFor(() => {
            expect(screen.getByText('Start')).toBeInTheDocument();
            expect(screen.getByText('End')).toBeInTheDocument();
        });

        const startDateInput = screen.getByRole('combobox', { name: 'Start date' });
        const startTimeInput = screen.getByRole('combobox', { name: 'Start time' });
        const endDateInput = screen.getByRole('combobox', { name: 'End date' });
        const endTimeInput = screen.getByRole('combobox', { name: 'End time' });

        expect(startDateInput).toBeInTheDocument();
        expect(startTimeInput).toBeInTheDocument();
        expect(endDateInput).toBeInTheDocument();
        expect(endTimeInput).toBeInTheDocument();

        const startTimeString = `${startDateInput.innerHTML}T${startTimeInput.getAttribute('value')}`;
        const endTimeString = `${endDateInput.innerHTML}T${endTimeInput.getAttribute('value')}`;

        console.log('Start Time String:', startTimeString);
        console.log('End Time String:', endTimeString);

        const startTime = new Date(startTimeString).getTime();
        const endTime = new Date(endTimeString).getTime();

        expect(endTime - startTime).toEqual(60 * 60 * 1000);
    });

    it('handles disabled state correctly', () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} disabled={true} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        expect(pillButton).toBeDisabled();
    });

    // it('shows remove button when onRemove is provided', () => {
    //     const mockOnRemove = vi.fn();

    //     render(
    //         <TestWrapper>
    //             <PillFilter {...defaultProps} onApply={mockOnApply} onRemove={mockOnRemove} />
    //         </TestWrapper>
    //     );

    //     const removeButton = screen.getByRole('button', { name: /Remove Time Range filter/ });
    //     expect(removeButton).toBeInTheDocument();
    // });

    // it('calls onRemove when remove button is clicked', async () => {
    //     const mockOnRemove = vi.fn();

    //     render(
    //         <TestWrapper>
    //             <PillFilter {...defaultProps} onApply={mockOnApply} onRemove={mockOnRemove} />
    //         </TestWrapper>
    //     );

    //     const removeButton = screen.getByRole('button', { name: /Remove Time Range filter/ });
    //     await userEvent.click(removeButton);

    //     expect(mockOnRemove).toHaveBeenCalled();
    // });

    it('closes popover when Cancel button is clicked', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            expect(screen.getByText('Cancel')).toBeInTheDocument();
        });

        const cancelButton = screen.getByRole('button', { name: 'Cancel' });
        await userEvent.click(cancelButton);

        await waitFor(() => {
            expect(screen.queryByText('Apply')).not.toBeInTheDocument();
        });
    });

    it('uses custom option label when provided', async () => {
        const propsWithCustomLabel = {
            ...defaultProps,
            customTimeRangeProps: {
                addCustomOption: true,
                customOptionLabel: 'Custom Range',
            },
        };

        render(
            <TestWrapper>
                <PillFilter {...propsWithCustomLabel} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            expect(screen.getByLabelText('Custom Range')).toBeInTheDocument();
        });
    });

    it('converts dates to UTC when calling onApply', async () => {
        const propsWithCustom = {
            ...defaultProps,
            customTimeRangeProps: {
                addCustomOption: true,
            },
            selectedValue: {
                key: TimespanKeys.Custom,
                start: new Date('2023-12-25T13:00:00Z'),
                end: new Date('2023-12-25T14:00:00Z'),
            },
        };

        render(
            <TestWrapper>
                <PillFilter {...propsWithCustom} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : Custom' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            expect(screen.getByText('Apply')).toBeInTheDocument();
        });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await userEvent.click(applyButton);

        expect(mockOnApply).toHaveBeenCalledWith({
            key: TimespanKeys.Custom,
            start: new Date('2023-12-25T13:00:00Z'),
            end: new Date('2023-12-25T14:00:00Z'),
        });

        // Verify the dates are passed to onApply
        const callArgs = mockOnApply.mock.calls[0][0];
        expect(callArgs.start).toBeInstanceOf(Date);
        expect(callArgs.end).toBeInstanceOf(Date);
    });

    it('resets to initial state when popover is cancelled', async () => {
        render(
            <TestWrapper>
                <PillFilter {...defaultProps} onApply={mockOnApply} />
            </TestWrapper>
        );

        const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Time Range : 1 Hour' });
        await userEvent.click(pillButton);

        await waitFor(() => {
            const sixHoursRadio = screen.getByLabelText('6 Hours');
            expect(sixHoursRadio).toBeInTheDocument();
        });

        // Change selection
        const sixHoursRadio = screen.getByLabelText('6 Hours');
        await userEvent.click(sixHoursRadio);

        // Cancel without applying
        const cancelButton = screen.getByRole('button', { name: 'Cancel' });
        await userEvent.click(cancelButton);

        // Reopen and verify original selection is restored
        await userEvent.click(pillButton);

        await waitFor(() => {
            const oneHourRadio = screen.getByLabelText('1 Hour');
            expect(oneHourRadio).toBeChecked();
        });
    });
});

import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi, type MockedFunction } from 'vitest';
import { ComboboxPillFilter, type ComboboxPillFilterProps, type LabelKeyPair } from '../PillFilter/ComboboxPillFilter';

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

const defaultOptions: LabelKeyPair[] = [
    { key: 'option1', label: 'Option 1' },
    { key: 'option2', label: 'Option 2' },
    { key: 'option3', label: 'Option 3' },
    { key: 'option4', label: 'Option 4' },
];

const defaultProps: ComboboxPillFilterProps = {
    label: 'Status',
    options: defaultOptions,
    onApply: vi.fn(),
    selectedKeys: ['option1'],
};

describe('ComboboxPillFilter', () => {
    let mockOnApply: MockedFunction<(keys: string[]) => void>;

    beforeEach(() => {
        mockOnApply = vi.fn();
        vi.clearAllMocks();
    });

    describe('Single Select Mode', () => {
        it('renders with basic props', () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            expect(pillButton).toBeInTheDocument();
            expect(within(pillButton).getByText('Status')).toBeInTheDocument();
            expect(within(pillButton).getByText('Option 1')).toBeInTheDocument();
        });

        it('displays custom display value when provided', () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} displayValue="Custom Display" />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            expect(pillButton).toBeInTheDocument();
            expect(within(pillButton).getByText('Custom Display')).toBeInTheDocument();
        });

        it('opens popover when clicked', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            // Check that the search box is present (indicates popover opened)
            expect(screen.getByPlaceholderText('Search')).toBeInTheDocument();

            // Check that options are displayed by looking for option roles
            const options = screen.getAllByRole('option');
            expect(options).toHaveLength(4);
            expect(screen.getByRole('option', { name: 'Option 1' })).toBeInTheDocument();
            expect(screen.getByRole('option', { name: 'Option 2' })).toBeInTheDocument();
            expect(screen.getByRole('option', { name: 'Option 3' })).toBeInTheDocument();
            expect(screen.getByRole('option', { name: 'Option 4' })).toBeInTheDocument();
        });

        it('shows selected option with checkmark', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');
            const option1Item = within(listBox).getByRole('option', { name: 'Option 1' });
            expect(option1Item).toBeInTheDocument();
            const option2Item = within(listBox).getByRole('option', { name: 'Option 2' });
            expect(option2Item).toBeInTheDocument();
            const option3Item = within(listBox).getByRole('option', { name: 'Option 3' });
            expect(option3Item).toBeInTheDocument();
            const option4Item = within(listBox).getByRole('option', { name: 'Option 4' });
            expect(option4Item).toBeInTheDocument();

            // The checkmark should be visible for selected item
            const option1checkmark = within(option1Item).getByTestId('option1');
            expect(option1checkmark.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for unselected items
            const option2checkmark = within(option2Item).getByTestId('option2');
            expect(option2checkmark.style.opacity).toEqual('0');
            const option3checkmark = within(option3Item).getByTestId('option3');
            expect(option3checkmark.style.opacity).toEqual('0');
            const option4checkmark = within(option4Item).getByTestId('option4');
            expect(option4checkmark.style.opacity).toEqual('0');
        });

        it('changes selection when different option is clicked', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');

            expect(within(listBox).getByText('Option 2')).toBeInTheDocument();
            const option2 = within(listBox).getByText('Option 2');
            await userEvent.click(option2);

            const option1Item = within(listBox).getByRole('option', { name: 'Option 1' });
            expect(option1Item).toBeInTheDocument();
            // The checkmark should not be visible for unselected item
            const checkmark1Icon = within(option1Item).getByTestId('option1');
            expect(checkmark1Icon.style.opacity).toEqual('0');

            const option2Item = within(listBox).getByRole('option', { name: 'Option 2' });
            expect(option2Item).toBeInTheDocument();
            // The checkmark should be visible for selected item
            const checkmark2Icon = within(option2Item).getByTestId('option2');
            expect(checkmark2Icon.style.opacity).toEqual('1');

            const applyButton = screen.getByRole('button', { name: 'Apply' });
            await userEvent.click(applyButton);

            expect(mockOnApply).toHaveBeenCalledWith(['option2']);
        });

        it('calls onApply when Apply button is clicked', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            const applyButton = screen.getByRole('button', { name: 'Apply' });
            await userEvent.click(applyButton);

            expect(mockOnApply).toHaveBeenCalledWith(['option1']);
        });
    });

    describe('Multi Select Mode', () => {
        const multiSelectProps = {
            ...defaultProps,
            multiSelect: true,
            selectedKeys: ['option1', 'option2'],
        };

        it('displays selection count in multi-select mode', () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...multiSelectProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : 2 of 4' });
            expect(pillButton).toBeInTheDocument();
            expect(within(pillButton).getByText('2 of 4')).toBeInTheDocument();
        });

        it('displays "All" when all options are selected', () => {
            const allSelectedProps = {
                ...multiSelectProps,
                selectedKeys: ['option1', 'option2', 'option3', 'option4'],
            };

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...allSelectedProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : All' });
            expect(within(pillButton).getByText('All')).toBeInTheDocument();
        });

        it('displays "All" when no options are selected', () => {
            const noSelectionProps = {
                ...multiSelectProps,
                selectedKeys: [],
            };

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...noSelectionProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : All' });
            expect(within(pillButton).getByText('All')).toBeInTheDocument();
        });

        it('allows multiple selections', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...multiSelectProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : 2 of 4' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Option 3')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');

            const option1Item = within(listBox).getByRole('option', { name: 'Option 1' });
            expect(option1Item).toBeInTheDocument();
            const option2Item = within(listBox).getByRole('option', { name: 'Option 2' });
            expect(option2Item).toBeInTheDocument();
            const option3Item = within(listBox).getByRole('option', { name: 'Option 3' });
            expect(option3Item).toBeInTheDocument();
            const option4Item = within(listBox).getByRole('option', { name: 'Option 4' });
            expect(option4Item).toBeInTheDocument();

            // The checkmark should be visible for selected items
            const option1checkmark = within(option1Item).getByTestId('option1');
            expect(option1checkmark.style.opacity).toEqual('1');
            const option2checkmark = within(option2Item).getByTestId('option2');
            expect(option2checkmark.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for unselected items
            const option3checkmark = within(option3Item).getByTestId('option3');
            expect(option3checkmark.style.opacity).toEqual('0');
            const option4checkmark = within(option4Item).getByTestId('option4');
            expect(option4checkmark.style.opacity).toEqual('0');

            // Select option 3
            const option3 = screen.getByText('Option 3');
            await userEvent.click(option3);

            // The checkmark should be visible for selected items
            const option1checkmarkAfter = within(option1Item).getByTestId('option1');
            expect(option1checkmarkAfter.style.opacity).toEqual('1');
            const option2checkmarkAfter = within(option2Item).getByTestId('option2');
            expect(option2checkmarkAfter.style.opacity).toEqual('1');
            const option3checkmarkAfter = within(option3Item).getByTestId('option3');
            expect(option3checkmarkAfter.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for unselected items
            const option4checkmarkAfter = within(option4Item).getByTestId('option4');
            expect(option4checkmarkAfter.style.opacity).toEqual('0');

            const applyButton = screen.getByRole('button', { name: 'Apply' });
            await userEvent.click(applyButton);

            expect(mockOnApply).toHaveBeenCalledWith(['option1', 'option2', 'option3']);
        });

        it('allows deselecting options', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...multiSelectProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : 2 of 4' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Option 1')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');

            const option1Item = within(listBox).getByRole('option', { name: 'Option 1' });
            expect(option1Item).toBeInTheDocument();
            const option2Item = within(listBox).getByRole('option', { name: 'Option 2' });
            expect(option2Item).toBeInTheDocument();
            const option3Item = within(listBox).getByRole('option', { name: 'Option 3' });
            expect(option3Item).toBeInTheDocument();
            const option4Item = within(listBox).getByRole('option', { name: 'Option 4' });
            expect(option4Item).toBeInTheDocument();

            // The checkmark should be visible for selected items
            const option1checkmark = within(option1Item).getByTestId('option1');
            expect(option1checkmark.style.opacity).toEqual('1');
            const option2checkmark = within(option2Item).getByTestId('option2');
            expect(option2checkmark.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for unselected items
            const option3checkmark = within(option3Item).getByTestId('option3');
            expect(option3checkmark.style.opacity).toEqual('0');
            const option4checkmark = within(option4Item).getByTestId('option4');
            expect(option4checkmark.style.opacity).toEqual('0');

            // Deselect option 1
            const option1 = screen.getByText('Option 1');
            await userEvent.click(option1);

            // The checkmark should be visible for selected items
            const option2checkmarkAfter = within(option2Item).getByTestId('option2');
            expect(option2checkmarkAfter.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for unselected items
            const option1checkmarkAfter = within(option1Item).getByTestId('option1');
            expect(option1checkmarkAfter.style.opacity).toEqual('0');
            const option3checkmarkAfter = within(option3Item).getByTestId('option3');
            expect(option3checkmarkAfter.style.opacity).toEqual('0');
            const option4checkmarkAfter = within(option4Item).getByTestId('option4');
            expect(option4checkmarkAfter.style.opacity).toEqual('0');

            const applyButton = screen.getByRole('button', { name: 'Apply' });
            await userEvent.click(applyButton);

            expect(mockOnApply).toHaveBeenCalledWith(['option2']);
        });
    });

    describe('All Option Feature', () => {
        const allOptionProps = {
            ...defaultProps,
            multiSelect: true,
            addAllOption: true,
            selectedKeys: [],
        };

        it('shows "All" option when addAllOption is enabled', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...allOptionProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : All' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            // Verify All option is in the list
            const listBox = screen.getByRole('listbox');
            const allOption = within(listBox).getByText('All');
            expect(allOption).toBeInTheDocument();
        });

        it('uses custom all option label when provided', async () => {
            const customAllProps = {
                ...allOptionProps,
                allOptionLabel: 'Select All Items',
            };

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...customAllProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', {
                name: 'Editor to filter the results by column value. Status : Select All Items',
            });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            // Verify All option is in the list
            const listBox = screen.getByRole('listbox');
            const allOption = within(listBox).getByText('Select All Items');
            expect(allOption).toBeInTheDocument();
        });

        it('selecting "All" option selects all available options', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...allOptionProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : All' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByText('Apply')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');
            const allOption = within(listBox).getByRole('option', { name: 'All' });

            await userEvent.click(allOption);

            const applyButton = screen.getByRole('button', { name: 'Apply' });
            await userEvent.click(applyButton);

            expect(mockOnApply).toHaveBeenCalledWith(['option1', 'option2', 'option3', 'option4']);
        });
    });

    describe('Search Functionality', () => {
        it('shows search box in the options list', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByPlaceholderText('Search')).toBeInTheDocument();
            });
        });

        it('filters options based on search input', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                expect(screen.getByPlaceholderText('Search')).toBeInTheDocument();
            });

            const searchBox = screen.getByPlaceholderText('Search');
            await userEvent.type(searchBox, '2');

            // Only Option 2 should be visible
            await waitFor(() => {
                const listBox = screen.getByRole('listbox');
                expect(within(listBox).getByText('Option 2')).toBeInTheDocument();
                expect(within(listBox).queryByText('Option 1')).not.toBeInTheDocument();
                expect(within(listBox).queryByText('Option 3')).not.toBeInTheDocument();
                expect(within(listBox).queryByText('Option 4')).not.toBeInTheDocument();
            });
        });
    });

    describe('Disabled State', () => {
        it('handles disabled state correctly', () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} disabled={true} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            expect(pillButton).toBeDisabled();
        });
    });

    describe('Remove Functionality', () => {
        it('shows remove button when onRemove is provided', () => {
            const mockOnRemove = vi.fn();

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} onRemove={mockOnRemove} />
                </TestWrapper>
            );

            const removeButton = screen.getByRole('button', { name: /Remove Status filter/ });
            expect(removeButton).toBeInTheDocument();
        });

        it('calls onRemove when remove button is clicked', async () => {
            const mockOnRemove = vi.fn();

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} onRemove={mockOnRemove} />
                </TestWrapper>
            );

            const removeButton = screen.getByRole('button', { name: /Remove Status filter/ });
            await userEvent.click(removeButton);

            expect(mockOnRemove).toHaveBeenCalled();
        });
    });

    describe('Cancel Functionality', () => {
        it('closes popover when Cancel button is clicked', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
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

        it('resets to initial state when popover is cancelled', async () => {
            render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status : Option 1' });
            await userEvent.click(pillButton);

            await waitFor(() => {
                const listBox = screen.getByRole('listbox');
                expect(within(listBox).getByText('Option 2')).toBeInTheDocument();
            });

            const listBox = screen.getByRole('listbox');

            // Change selection
            const option2 = within(listBox).getByText('Option 2');
            await userEvent.click(option2);

            // Cancel without applying
            const cancelButton = screen.getByRole('button', { name: 'Cancel' });
            await userEvent.click(cancelButton);

            // Wait for popover to close completely
            await waitFor(() => {
                expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
                expect(screen.queryByText('Apply')).not.toBeInTheDocument();
            });

            // Reopen and verify original selection is restored
            await userEvent.click(pillButton);

            // Wait for the new listbox to appear
            await waitFor(() => {
                expect(screen.getByRole('listbox', { name: /status options/i, hidden: true })).toBeInTheDocument();
            });

            // Now get the new listbox
            const listBox2 = screen.getByRole('listbox', { name: /status options/i, hidden: true });

            const option1Item = within(listBox2).getByRole('option', { name: /option 1/i, hidden: true });
            const option2Item = within(listBox2).getByRole('option', { name: /option 2/i, hidden: true });
            const option3Item = within(listBox2).getByRole('option', { name: /option 3/i, hidden: true });
            const option4Item = within(listBox2).getByRole('option', { name: /option 4/i, hidden: true });

            expect(option1Item).toBeInTheDocument();
            expect(option1Item).toBeInTheDocument();
            expect(option2Item).toBeInTheDocument();
            expect(option3Item).toBeInTheDocument();
            expect(option4Item).toBeInTheDocument();

            // The checkmark should be visible for the original selected item
            const checkmarkIcon = within(option1Item).getByTestId('option1');
            expect(checkmarkIcon.style.opacity).toEqual('1');

            // The checkmark should NOT be visible for the unselected items
            const checkmarkIcon2 = within(option2Item).getByTestId('option2');
            expect(checkmarkIcon2.style.opacity).toEqual('0');
            const checkmarkIcon3 = within(option3Item).getByTestId('option3');
            expect(checkmarkIcon3.style.opacity).toEqual('0');
            const checkmarkIcon4 = within(option4Item).getByTestId('option4');
            expect(checkmarkIcon4.style.opacity).toEqual('0');
        });
    });

    describe('Edge Cases', () => {
        it('handles empty options array', () => {
            const emptyOptionsProps = {
                ...defaultProps,
                options: [],
                selectedKeys: [],
            };

            render(
                <TestWrapper>
                    <ComboboxPillFilter {...emptyOptionsProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            const pillButton = screen.getByRole('button', { name: 'Editor to filter the results by column value. Status :' });
            expect(pillButton).toBeInTheDocument();
        });

        it('updates when selectedKeys prop changes', async () => {
            const { rerender } = render(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} />
                </TestWrapper>
            );

            expect(screen.getByText('Option 1')).toBeInTheDocument();

            // Update props
            rerender(
                <TestWrapper>
                    <ComboboxPillFilter {...defaultProps} onApply={mockOnApply} selectedKeys={['option2']} />
                </TestWrapper>
            );

            await waitFor(() => {
                expect(screen.getByText('Option 2')).toBeInTheDocument();
            });
        });
    });
});

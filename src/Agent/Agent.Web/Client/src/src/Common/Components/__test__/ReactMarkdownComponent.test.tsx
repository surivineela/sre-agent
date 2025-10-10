import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ReactMarkdownComponent from '../ReactMarkdownComponent';

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

describe('ReactMarkdownComponent', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    describe('Basic rendering and structure', () => {
        const basicTableMarkdown = `| Name | Age | City |
|------|-----|------|
| John | 25  | NYC  |
| Jane | 30  | LA   |
| Bob  | 35  | Chicago |`;

        describe('Basic Table Functionality', () => {
            it('renders HTML tables with proper structure and content', () => {
                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={basicTableMarkdown} />
                    </TestWrapper>
                );

                const tableElement = screen.getByRole('table');
                expect(tableElement).toBeInTheDocument();

                const headers = screen.getAllByRole('columnheader');
                expect(headers).toHaveLength(3);
                expect(headers[0]).toHaveTextContent('Name');
                expect(headers[1]).toHaveTextContent('Age');
                expect(headers[2]).toHaveTextContent('City');

                const thead = container.querySelector('thead');
                const tbody = container.querySelector('tbody');
                expect(thead).toBeInTheDocument();
                expect(tbody).toBeInTheDocument();

                expect(screen.getByText('John')).toBeInTheDocument();
                expect(screen.getByText('25')).toBeInTheDocument();
                expect(screen.getByText('NYC')).toBeInTheDocument();
                expect(screen.getByText('Jane')).toBeInTheDocument();
                expect(screen.getByText('30')).toBeInTheDocument();
                expect(screen.getByText('LA')).toBeInTheDocument();
                expect(screen.getByText('Bob')).toBeInTheDocument();
                expect(screen.getByText('35')).toBeInTheDocument();
                expect(screen.getByText('Chicago')).toBeInTheDocument();
            });
        });

        describe('Empty and Special Cases', () => {
            it('handles empty table cells with default dash', () => {
                const tableWithEmptyCell = `| Name | Value | Status |
|------|-------|--------|
| Test |       | Active |
| Full | 123   |        |
| Both | 456   | Ready  |`;

                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={tableWithEmptyCell} />
                    </TestWrapper>
                );

                const table = screen.getByRole('table');
                expect(table).toBeInTheDocument();

                const headers = screen.getAllByRole('columnheader');
                expect(headers).toHaveLength(3);
                expect(headers[0]).toHaveTextContent('Name');
                expect(headers[1]).toHaveTextContent('Value');
                expect(headers[2]).toHaveTextContent('Status');

                const thead = container.querySelector('thead');
                const tbody = container.querySelector('tbody');
                expect(thead).toBeInTheDocument();
                expect(tbody).toBeInTheDocument();

                const dataRows = tbody?.querySelectorAll('tr');
                expect(dataRows?.length).toBe(3);

                dataRows?.forEach(row => {
                    const cells = row.querySelectorAll('td');
                    expect(cells.length).toBe(3);
                });

                expect(screen.getByText('Test')).toBeInTheDocument();
                expect(screen.getByText('Full')).toBeInTheDocument();
                expect(screen.getByText('123')).toBeInTheDocument();
                expect(screen.getByText('Active')).toBeInTheDocument();
                expect(screen.getByText('Ready')).toBeInTheDocument();

                // Check for empty cells (should have dash placeholders or be empty)
                const cells = screen.getAllByRole('cell');
                expect(cells.length).toBe(9);

                const emptyCells = Array.from(cells).filter(cell => cell.textContent === '' || cell.textContent === '-');
                expect(emptyCells.length).toBeGreaterThan(0);
            });

            it('handles single column tables', () => {
                const singleColumnTable = `| Status |
|--------|
| Active |
| Error  |
| Warning|`;

                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={singleColumnTable} />
                    </TestWrapper>
                );

                const table = screen.getByRole('table');
                expect(table).toBeInTheDocument();
                const thead = container.querySelector('thead');
                const tbody = container.querySelector('tbody');
                expect(thead).toBeInTheDocument();
                expect(tbody).toBeInTheDocument();

                expect(screen.getByText('Active')).toBeInTheDocument();
                expect(screen.getByText('Error')).toBeInTheDocument();
                expect(screen.getByText('Warning')).toBeInTheDocument();

                const cells = screen.getAllByRole('cell');
                expect(cells.length).toBe(3);
            });

            it('handles single row tables', () => {
                const singleRowTable = `| Name | Age | City |
|------|-----|------|
| John | 25  | NYC  |`;

                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={singleRowTable} />
                    </TestWrapper>
                );

                const table = screen.getByRole('table');
                expect(table).toBeInTheDocument();

                const headers = screen.getAllByRole('columnheader');
                expect(headers).toHaveLength(3);
                expect(headers[0]).toHaveTextContent('Name');
                expect(headers[1]).toHaveTextContent('Age');
                expect(headers[2]).toHaveTextContent('City');

                const thead = container.querySelector('thead');
                const tbody = container.querySelector('tbody');
                expect(thead).toBeInTheDocument();
                expect(tbody).toBeInTheDocument();

                const headerRow = thead?.querySelector('tr');
                const headerCells = headerRow?.querySelectorAll('th');
                expect(headerCells?.length).toBe(3);

                expect(screen.getByText('John')).toBeInTheDocument();
                expect(screen.getByText('25')).toBeInTheDocument();
                expect(screen.getByText('NYC')).toBeInTheDocument();

                const cells = screen.getAllByRole('cell');
                expect(cells.length).toBe(3);
            });
        });

        describe('Error Recovery and Edge Cases', () => {
            it('handles table with only headers (no data rows)', () => {
                const headerOnlyTable = `| Name | Age | City |
|------|-----|------|`;

                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={headerOnlyTable} />
                    </TestWrapper>
                );

                const table = screen.getByRole('table');
                expect(table).toBeInTheDocument();

                const headers = screen.getAllByRole('columnheader');
                expect(headers).toHaveLength(3);
                expect(headers[0]).toHaveTextContent('Name');
                expect(headers[1]).toHaveTextContent('Age');
                expect(headers[2]).toHaveTextContent('City');

                const thead = container.querySelector('thead');
                const tbody = container.querySelector('tbody');
                expect(thead).toBeInTheDocument();
                expect(tbody).toBeInTheDocument();

                const headerRow = thead?.querySelector('tr');
                const headerCells = headerRow?.querySelectorAll('th');
                expect(headerCells?.length).toBe(3);
                expect(headerCells?.[0]).toHaveTextContent('Name');
                expect(headerCells?.[1]).toHaveTextContent('Age');
                expect(headerCells?.[2]).toHaveTextContent('City');

                const dataRows = tbody?.querySelectorAll('tr');
                expect(dataRows?.length).toBe(0);

                const cells = screen.queryAllByRole('cell');
                expect(cells.length).toBe(0);
            });

            it('handles completely empty table', () => {
                const emptyTable = `|  |  |  |
|--|--|--|
|  |  |  |`;

                const { container } = render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={emptyTable} />
                    </TestWrapper>
                );

                const table = container.querySelector('table');
                expect(table).toBeInTheDocument();
                const cells = screen.getAllByRole('cell');
                expect(cells.length).toBeGreaterThan(0);
            });
        });

        describe('Integration with Other Markdown Elements', () => {
            it('renders tables alongside other markdown elements', () => {
                const mixedContent = `# Service Status Report

Here's the current status of our services:

${basicTableMarkdown}

## Analysis

The services are performing well with the following observations:

- All services are operational
- Average age is **30 years**
- Geographic distribution across major cities

> **Note**: This data is updated every hour.`;

                render(
                    <TestWrapper>
                        <ReactMarkdownComponent content={mixedContent} />
                    </TestWrapper>
                );

                expect(screen.getByRole('heading', { level: 1, name: 'Service Status Report' })).toBeInTheDocument();
                expect(screen.getByText('Name')).toBeInTheDocument();
                expect(screen.getByText('John')).toBeInTheDocument();
                expect(screen.getByRole('heading', { level: 2, name: 'Analysis' })).toBeInTheDocument();
                expect(screen.getByText('30 years')).toBeInTheDocument();
                expect(screen.getByText('Note')).toBeInTheDocument();
            });
        });
    });
});

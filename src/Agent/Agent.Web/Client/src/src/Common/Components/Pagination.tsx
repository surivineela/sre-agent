import { Button, tokens } from '@fluentui/react-components';
import { ChevronLeft20Regular, ChevronRight20Regular } from '@fluentui/react-icons';
import React from 'react';

export interface PaginationProps {
    currentPage: number;
    totalPages: number;
    onPageChange: (page: number) => void;
}

export const Pagination: React.FC<PaginationProps> = ({ currentPage, totalPages, onPageChange }) => {
    if (totalPages <= 1) return null;
    const getVisiblePages = () => {
        const maxElements = 5;
        const pages = [];

        if (totalPages <= maxElements) {
            for (let i = 1; i <= totalPages; i++) {
                pages.push(i);
            }
        } else {
            if (currentPage <= 3) {
                pages.push(1, 2, 3, '...', totalPages);
            } else if (currentPage >= totalPages - 2) {
                pages.push(1, '...', totalPages - 2, totalPages - 1, totalPages);
            } else {
                pages.push(1, '...', currentPage, '...', totalPages);
            }
        }

        return pages;
    };

    const visiblePages = getVisiblePages();

    return (
        <div
            style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'flex-start',
                gap: '4px',
                marginTop: '16px',
                marginBottom: '8px',
            }}
        >
            <Button
                appearance="subtle"
                size="small"
                disabled={currentPage === 1}
                onClick={() => onPageChange(currentPage - 1)}
                icon={<ChevronLeft20Regular />}
                style={{
                    minWidth: '32px',
                    height: '32px',
                }}
            />

            {visiblePages.map((page, index) => {
                if (page === '...') {
                    return (
                        <span
                            key={`ellipsis-${index}`}
                            style={{
                                padding: '0 8px',
                                color: tokens.colorNeutralForeground2,
                                fontSize: tokens.fontSizeBase300,
                            }}
                        >
                            ...
                        </span>
                    );
                }

                const pageNumber = page as number;
                const isActive = pageNumber === currentPage;
                return (
                    <Button
                        key={pageNumber}
                        appearance="transparent"
                        size="small"
                        onClick={() => onPageChange(pageNumber)}
                        style={{
                            minWidth: '32px',
                            height: '32px',
                            backgroundColor: 'transparent',
                            color: isActive ? tokens.colorNeutralForeground1 : tokens.colorBrandForeground1,
                            border: 'none',
                            fontWeight: isActive ? '600' : '400',
                        }}
                    >
                        {pageNumber}
                    </Button>
                );
            })}

            <Button
                appearance="subtle"
                size="small"
                disabled={currentPage === totalPages}
                onClick={() => onPageChange(currentPage + 1)}
                icon={<ChevronRight20Regular />}
                style={{
                    minWidth: '32px',
                    height: '32px',
                }}
            />
        </div>
    );
};

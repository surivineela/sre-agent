import { Field, FieldProps, FieldSlots, InfoLabel, LabelProps } from '@fluentui/react-components';

export type FieldWrapperProps = Pick<FieldProps, 'orientation' | 'required' | 'label'> & {
    children: React.ReactNode;
    tooltip?: string;
    error?: string;
    // use only for extra customization that may need to also override existing Field props
    fieldProps?: Partial<FieldSlots>;
};

export const FieldWrapper: React.FC<FieldWrapperProps> = ({
    label,
    required,
    tooltip,
    error,
    orientation = 'horizontal',
    children,
    fieldProps,
}) => {
    return (
        <Field
            label={
                tooltip
                    ? {
                          // Setting children to a render function allows you to replace the entire slot.
                          // The first param is the component for the slot (Label), which we're ignoring to use InfoLabel instead.
                          // The second param are the props for the slot, which need to be passed to the InfoLabel.
                          children: (_: unknown, slotProps: LabelProps) => (
                              <InfoLabel {...slotProps} info={tooltip}>
                                  {label}
                              </InfoLabel>
                          ),
                      }
                    : label
            }
            required={required}
            validationState={error ? 'error' : 'none'}
            validationMessage={error}
            orientation={orientation}
            {...fieldProps}
        >
            {children}
        </Field>
    );
};

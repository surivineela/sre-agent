import { Stack } from '@fluentui/react';
import { Outlet } from 'react-router-dom';
import SideNav from './SideNav';

const EditLayOut = () => {
    return (
        <Stack horizontal enableScopedSelectors >
            <Stack.Item grow={1}>
                <SideNav />
            </Stack.Item>
            <Stack.Item grow={4}>
                <Outlet />
            </Stack.Item>
        </Stack>
    )
}

export default EditLayOut;
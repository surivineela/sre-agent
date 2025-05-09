import React from 'react';
import { IEnvironmentInfo } from '../Models/IEnvironments';

export const EnvironmentContext = React.createContext<IEnvironmentInfo>({} as IEnvironmentInfo);
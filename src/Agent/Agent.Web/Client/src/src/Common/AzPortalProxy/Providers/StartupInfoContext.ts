import React from 'react';
import { IEnvironmentInfo } from '../Models/IEnvironmentInfo';

export const EnvironmentContext = React.createContext<IEnvironmentInfo>({} as IEnvironmentInfo);

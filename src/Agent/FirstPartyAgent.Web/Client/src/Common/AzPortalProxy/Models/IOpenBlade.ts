import { IDataMessage } from "./IDataMessage";


export interface IOpenBlade {
    detailBlade: string;
    detailBladeInputs: any;
    extension: string;
    asContextBlade?: boolean;
    asSubJourney?: boolean;
}

export interface IBladeClosed {
    reason: 'userNavigation' | 'childClosedSelf';
    data: any;
}

export type IOpenBladeRequest = IOpenBlade & IDataMessage;
export type IBladeClosedResult = IBladeClosed & IDataMessage;

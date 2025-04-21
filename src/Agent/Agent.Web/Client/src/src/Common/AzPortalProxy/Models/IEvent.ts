export interface IEvent {
    data: IData;
    origin: string;
}

export interface IData {
    signature: string;
    kind: string;
    data: any;
}
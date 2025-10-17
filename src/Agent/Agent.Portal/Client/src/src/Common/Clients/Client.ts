export class Client {
    private _endpoint: string;

    constructor(endpoint: string) {
        this._endpoint = endpoint;
        console.log(this._endpoint);
    }
}

import { TelemetrySource } from '../Constants/Telemetry';

export class Client {
    protected telemetrySource: TelemetrySource;

    constructor(telemetrySource: TelemetrySource) {
        this.telemetrySource = telemetrySource;
    }
}

import { TelemetryEvent } from '../Contracts/Telemetry';

export class TelemetryClient {
    public static logEvent(event: TelemetryEvent): void {
        // TODO: Hook up to telemetry/ILogger API
        console.log(event);
    }
}

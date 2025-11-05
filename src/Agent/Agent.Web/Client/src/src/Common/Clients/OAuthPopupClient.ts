import { popupId, redirectUrl } from './OAuthService';

export interface IOAuthPopup {
    [x: string]: any;
    loginPromise: Promise<any>;
}

export interface LoginResult {
    [x: string]: any;
    code?: string;
    error?: string;
    timerId?: number;
}

export interface OAuthPopupOptions {
    consentUrl: string;
}

export class OAuthPopup implements IOAuthPopup {
    public loginPromise: Promise<LoginResult>;

    private _popupId: string;
    private _popupWindow: Window | undefined;
    private _timer: any;
    private _msg?: string;
    constructor(options: OAuthPopupOptions) {
        const { consentUrl } = options;
        this._popupId = popupId;
        this.loginPromise = this.login(consentUrl);
    }

    private login = async (consentUrl: string): Promise<LoginResult> => {
        const authUrl = new URL(consentUrl);

        const windowWidth = 600;
        const windowHeight = 600;
        const windowOptions = Object.entries({
            scrollbars: true,
            resizable: true,
            width: windowWidth,
            height: windowHeight,
            popup: true,
            top: screen.height / 2 - 600 / 2,
            left: screen.width / 2 - 600 / 2,
        })
            .map(([key, value]) => `${key}=${value}`)
            .join(',');
        const oAuthWindow = window.open(authUrl.href, this._popupId, windowOptions);
        if (!oAuthWindow) {
            throw new Error('The browser has blocked the popup window.');
        }
        this._popupWindow = oAuthWindow;

        if (!this._popupWindow) {
            throw new Error('The browser has blocked the popup window.');
        }

        let timeoutCounter = 0;
        const listener = (event: MessageEvent) => {
            const origin = event.origin;
            const redirectOrigin = new URL(redirectUrl).origin;
            if (origin !== redirectOrigin) {
                return;
            }
            this._msg = decodeURIComponent(event.data);
            window.removeEventListener('message', listener);
            this._popupWindow?.close();
        };
        window.addEventListener('message', listener);
        return new Promise<LoginResult>((resolve, reject) => {
            this._timer = window.setInterval(() => {
                timeoutCounter++;
                this.handlePopup(resolve, reject, timeoutCounter);
            }, 1000);
        });
    };

    private handlePopup(resolve: any, reject: any, timeoutCounter: number) {
        if (this._popupWindow?.closed) {
            const storageValue = this._msg ? decodeURIComponent(this._msg) : undefined;

            if (storageValue) {
                resolve(JSON.parse(storageValue));
            } else {
                reject({
                    name: 'Error',
                    message: 'The browser is closed',
                });
            }
            clearInterval(this._timer);
        } else if (timeoutCounter >= 300) {
            reject({
                name: 'Error',
                message: 'Timeout',
            });
            clearInterval(this._timer);
        }
    }
}

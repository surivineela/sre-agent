export default class Url {
    public static getParameterByName(url: string | null, name: string) {
        let urlFull = url;
        if (urlFull === null) {
          urlFull = window.location.href;
        }
      
        if (!name) {
          return null;
        }
      
        const sanatizedName = name.replace(/[[\]]/g, '\\$&');
        const regex = new RegExp(`[?&]${sanatizedName}(=([^&#]*)|&|#|$)`, 'i');
        const results = regex.exec(urlFull);
      
        if (!results) {
          return null;
        }
      
        if (!results[2]) {
          return '';
        }
      
        return decodeURIComponent(results[2].replace(/\+/g, ' '));
      }
}
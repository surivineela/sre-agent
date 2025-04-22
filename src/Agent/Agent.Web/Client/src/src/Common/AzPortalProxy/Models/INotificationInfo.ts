export type INotificationState = 'start' | 'success' | 'fail'; 

export interface INotificationInfo {
  id: string;
  state: INotificationState; 
  title: string;
  description: string;
}
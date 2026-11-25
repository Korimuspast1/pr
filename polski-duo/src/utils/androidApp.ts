interface AndroidAppBridge {
  getAppVersion: () => string;
  openExternalUrl: (url: string) => void;
  scheduleDailyReminder: (hour: number, minute: number) => boolean;
  cancelDailyReminder: () => void;
  sendTestNotification: () => void;
}

declare global {
  interface Window {
    PolskiAndroidApp?: AndroidAppBridge;
  }
}

export const APK_DOWNLOAD_URL =
  'https://github.com/Korimuspast1/pr/raw/arena/75344e87-pr/polski-duo/release/PolskiOrzel.apk';
export const GITHUB_COMMIT_API =
  'https://api.github.com/repos/Korimuspast1/pr/commits/arena/75344e87-pr';
export const WEB_APP_VERSION = '1.4.0';

export function getInstalledAppVersion(): string {
  if (typeof window !== 'undefined' && window.PolskiAndroidApp) {
    try {
      return window.PolskiAndroidApp.getAppVersion();
    } catch {
      return WEB_APP_VERSION;
    }
  }
  return `${WEB_APP_VERSION} (Web)`;
}

export function openApkDownload(): void {
  if (typeof window !== 'undefined' && window.PolskiAndroidApp) {
    window.PolskiAndroidApp.openExternalUrl(APK_DOWNLOAD_URL);
    return;
  }
  window.open(APK_DOWNLOAD_URL, '_blank', 'noopener,noreferrer');
}

export function scheduleAndroidReminder(timeStr: string): boolean {
  const [hStr, mStr] = timeStr.split(':');
  const hour = Number.parseInt(hStr ?? '20', 10) || 20;
  const minute = Number.parseInt(mStr ?? '0', 10) || 0;

  if (typeof window !== 'undefined' && window.PolskiAndroidApp) {
    return window.PolskiAndroidApp.scheduleDailyReminder(hour, minute);
  }
  if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
    Notification.requestPermission().catch(() => {});
  }
  return true;
}

export function cancelAndroidReminder(): void {
  if (typeof window !== 'undefined' && window.PolskiAndroidApp) {
    window.PolskiAndroidApp.cancelDailyReminder();
  }
}

export function triggerTestReminder(): boolean {
  if (typeof window !== 'undefined' && window.PolskiAndroidApp) {
    window.PolskiAndroidApp.sendTestNotification();
    return true;
  }
  if (typeof Notification !== 'undefined') {
    if (Notification.permission === 'granted') {
      new Notification('PolskiOrzeł ждёт тебя!', {
        body: 'Повтори 5 польских слов за 2 минуты и сохрани ударный режим.',
      });
      return true;
    }
    Notification.requestPermission().then((p) => {
      if (p === 'granted') {
        new Notification('PolskiOrzeł ждёт тебя!', {
          body: 'Напоминания включены!',
        });
      }
    });
    return true;
  }
  return false;
}

export interface UpdateCheckResult {
  ok: boolean;
  latestSha?: string;
  latestMessage?: string;
  latestDate?: string;
  error?: string;
}

export async function checkGitHubApkUpdate(): Promise<UpdateCheckResult> {
  try {
    const res = await fetch(GITHUB_COMMIT_API, {
      headers: { Accept: 'application/vnd.github+json' },
    });
    if (!res.ok) {
      return { ok: false, error: `GitHub ответил кодом ${res.status}` };
    }
    const data = (await res.json()) as {
      sha?: string;
      commit?: { message?: string; author?: { date?: string } };
    };
    return {
      ok: true,
      latestSha: (data.sha ?? '').slice(0, 7),
      latestMessage: (data.commit?.message ?? 'Обновление PolskiOrzeł').split('\n')[0],
      latestDate: data.commit?.author?.date
        ? new Date(data.commit.author.date).toLocaleString('ru-RU')
        : undefined,
    };
  } catch {
    return {
      ok: false,
      error: 'Нет соединения с GitHub. Проверьте интернет и попробуйте снова.',
    };
  }
}

import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.fakegpspro.app',
  appName: 'FakeGpsPro',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
};

export default config;

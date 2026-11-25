import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.polskiorzel.app',
  appName: 'PolskiOrzeł',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
  android: {
    backgroundColor: '#FFFFFF',
  },
};

export default config;

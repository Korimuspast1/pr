import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.pocketai.studio',
  appName: 'PocketAI Studio',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
};

export default config;

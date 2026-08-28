import { defineConfig } from '@rsbuild/core'
import { pluginReact } from '@rsbuild/plugin-react'

export default defineConfig({
  plugins: [pluginReact()],
  source: {
    entry: {
      index: './src/main.tsx',
    },
  },
  html: {
    title: 'HealthPulse',
    tags: [
      {
        tag: 'link',
        attrs: { href: 'styles.css', rel: 'stylesheet' },
        head: true,
        publicPath: true,
      },
      {
        tag: 'meta',
        attrs: { name: 'theme-color', content: '#f7f5ef' },
        head: true,
      },
    ],
  },
  output: {
    assetPrefix: '/app/',
    cleanDistPath: true,
    copy: [{ from: './src/styles.css', to: 'styles.css' }],
    distPath: {
      root: '../wwwroot/app',
    },
  },
  server: {
    base: '/',
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5252',
      '/login': 'http://localhost:5252',
    },
  },
})

import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Asterion',
  description: 'Access all of Modrinth within Discord',
  base: '/Asterion/',
  cleanUrls: true,
  lastUpdated: true,
  head: [['link', { rel: 'icon', type: 'image/png', href: '/Asterion/favicon.png' }]],

  themeConfig: {
    logo: { light: '/logo.png', dark: '/logo-dark.png' },

    nav: [
      { text: 'Quick start', link: '/quick-start' },
      { text: 'Commands', link: '/commands' },
      { text: 'Invite', link: 'https://discord.com/api/oauth2/authorize?client_id=986993814671614094&permissions=537054208&scope=bot%20applications.commands' }
    ],

    sidebar: [
      {
        text: 'Getting started',
        items: [
          { text: 'Quick start', link: '/quick-start' },
          { text: 'Commands', link: '/commands' }
        ]
      },
      {
        text: 'Guides',
        items: [
          { text: 'Project subscriptions', link: '/guides/subscriptions' },
          { text: 'Following creators', link: '/guides/following' },
          { text: 'The /manage panel', link: '/guides/manage' },
          { text: 'Charts', link: '/guides/charts' }
        ]
      },
      {
        text: 'More',
        items: [
          { text: 'Self-hosting', link: '/self-hosting' },
          { text: 'FAQ', link: '/faq' },
          { text: 'Privacy policy', link: '/privacy' },
          { text: 'Terms of service', link: '/terms' }
        ]
      }
    ],

    search: { provider: 'local' },

    socialLinks: [{ icon: 'github', link: 'https://github.com/Zechiax/Asterion' }],

    editLink: {
      pattern: 'https://github.com/Zechiax/Asterion/edit/main/docs/:path'
    }
  }
})

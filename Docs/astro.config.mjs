// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import fs from 'node:fs';

const catnipGrammar = JSON.parse(
	fs.readFileSync(new URL('../EditorConfigs/catnip.tmLanguage.json', import.meta.url), 'utf8')
);
const catasmGrammar = JSON.parse(
	fs.readFileSync(new URL('../EditorConfigs/catasm.tmLanguage.json', import.meta.url), 'utf8')
);

// https://astro.build/config
export default defineConfig({
	integrations: [
		starlight({
			title: 'Cat Machine Docs',
			logo: {
				src: './public/favicon.svg',
				alt: 'Cat Machine logo',
			},
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/Serble/CatMachine' }],
			components: {
				Head: './src/components/Head.astro',
				SocialIcons: './src/components/SocialIcons.astro',
			},
			expressiveCode: {
				shiki: {
					langs: [
						{
							...catnipGrammar,
							name: 'catnip',
							aliases: ['nip'],
							embeddedLangs: ['catasm'],
						},
						{
							...catasmGrammar,
							name: 'catasm',
							aliases: ['cat', 'asm'],
						},
					],
					langAlias: {
						cat: 'catasm',
						asm: 'catasm',
						catnip: 'catnip',
						nip: 'catnip',
					},
				},
			},
			sidebar: [
				{
					label: 'VM',
					items: [
						{ label: 'Registers', slug: 'vm/registers' },
						{ label: 'Instructions', slug: 'vm/instructions' },
						{ label: 'Memory', slug: 'vm/memory' },
						{ label: 'Virtual Mode', slug: 'vm/virtual-mode' },
						{ label: 'Interrupts', slug: 'vm/interrupts' },
						{ label: 'Serial Protocol', slug: 'vm/serial-protocol' },
					],
				},
				{
					label: 'Hardware',
					items: [
						{ label: 'Hardware Manager', slug: 'hardware/hardwareman' },
						{ label: 'Raylib PPU', slug: 'hardware/raylibppu' },
						{ label: 'Hardware Timer', slug: 'hardware/hardwaretimer' },
						{ label: 'Disk Device', slug: 'hardware/diskdevice' },
						{ label: 'Virtual Network Card', slug: 'hardware/vnic' },
						{ label: 'Hello World Device', slug: 'hardware/helloworlddevice' },
					],
				},
				{
					label: 'Assembly',
					items: [
						{ label: 'Assembly Reference', slug: 'assembly/catasm-reference' },
						{ label: 'Hello World Tutorial', slug: 'assembly/hello-world' },
						{ label: 'Fibonacci Tutorial', slug: 'assembly/fibonacci' },
					],
				},
				{
					label: 'Catnip',
					items: [
						{ label: 'Catnip Reference', slug: 'catnip/catnip-reference' },
						{ label: 'Hello World Tutorial', slug: 'catnip/hello-world' },
						{ label: 'Fibonacci Tutorial', slug: 'catnip/fibonacci' },
					],
				},
			],
		}),
	],
});

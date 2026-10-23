// Bootstrap 6 Sass writes its CSS custom properties unprefixed; the "bs-" prefix is added
// here, the same way Bootstrap's own dist build does. Already-prefixed names are kept.
// Minification runs only for the production (site.min.css) pass.
module.exports = (ctx) => ({
    plugins: [
        require('postcss-prefix-custom-properties')({
            prefix: 'bs-',
            ignore: [/^--bs-/]
        }),
        require('autoprefixer')({ cascade: false }),
        ...(ctx.env === 'production' ? [require('cssnano')({ preset: ['default', { calc: false }] })] : []),
    ],
});

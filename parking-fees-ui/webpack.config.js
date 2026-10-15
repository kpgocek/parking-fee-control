const path = require('path');
const MOD = {
  ...require('./mod.json'),
  ...(process.env.PARKING_FEE_VERSION ? { version: process.env.PARKING_FEE_VERSION } : {}),
};
const TerserPlugin = require('terser-webpack-plugin');

const OUTPUT_DIR = './output';

const banner = `
 * Cities: Skylines II UI Module
 *
 * Id: ${MOD.id}
 * Author: ${MOD.author}
 * Version: ${MOD.version}
 * Dependencies: ${MOD.dependencies.join(',')}
`;

module.exports = {
  mode: 'production',
  stats: 'errors-warnings',
  entry: {
    [MOD.id]: './src/index.tsx',
  },
  externalsType: 'window',
  externals: {
    react: 'React',
    'react-dom': 'ReactDOM',
    'cs2/modding': 'cs2/modding',
    'cs2/api': 'cs2/api',
    'cs2/bindings': 'cs2/bindings',
    'cs2/l10n': 'cs2/l10n',
    'cs2/ui': 'cs2/ui',
    'cs2/input': 'cs2/input',
    'cs2/utils': 'cs2/utils',
    'cohtml/cohtml': 'cohtml/cohtml',
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: 'ts-loader',
        exclude: /node_modules/,
      },
      {
        test: /\.(png|jpe?g|gif|svg)$/i,
        type: 'asset/resource',
        generator: {
          filename: 'images/[name][ext][query]',
        },
      },
    ],
  },
  resolve: {
    extensions: ['.tsx', '.ts', '.js'],
    modules: ['node_modules', path.join(__dirname, 'src')],
    alias: {
      'mod.json': path.resolve(__dirname, 'mod.json'),
    },
  },
  output: {
    path: path.resolve(__dirname, OUTPUT_DIR),
    filename: '[name].mjs',
    clean: true,
    library: {
      type: 'module',
    },
    publicPath: `coui://ui-mods/`,
  },
  optimization: {
    minimize: true,
    minimizer: [
      new TerserPlugin({
        terserOptions: {
          format: {
            comments: /^\**!|@preserve|@license|@cc_on/i,
          },
        },
        extractComments: {
          banner: () => banner,
        },
      }),
    ],
  },
  experiments: {
    outputModule: true,
  },
  plugins: [
    {
      apply(compiler) {
        compiler.hooks.thisCompilation.tap('ModMetadataPlugin', (compilation) => {
          compilation.hooks.processAssets.tap(
            { name: 'ModMetadataPlugin', stage: compiler.webpack.Compilation.PROCESS_ASSETS_STAGE_ADDITIONAL },
            () => compilation.emitAsset('mod.json', new compiler.webpack.sources.RawSource(JSON.stringify(MOD, null, 2) + '\n')),
          );
        });
      },
    },
  ],
};

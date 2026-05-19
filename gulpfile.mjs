import gulp from 'gulp';
import sourcemaps from 'gulp-sourcemaps';
import * as sass from 'sass';
import gulpSass from 'gulp-sass';
import autoprefixer from 'gulp-autoprefixer';
import cleanCSS from 'gulp-clean-css';
import rename from 'gulp-rename';
import postcss from 'gulp-postcss';
import postcssImport from 'postcss-import';
import terser from 'gulp-terser';
const sassCompiler = gulpSass(sass);
import data from './package.json' assert { type: 'json' }

// Watch Task
function watchTask() {
  gulp.watch(['wwwroot/assets/scss/**/*'], gulp.series(scssTask));
}

// SCSS Task
function scssTask() {
  const scssFiles = 'wwwroot/assets/scss/**/*.scss';
  const cssDest = 'wwwroot/assets/css';

  return gulp.src(scssFiles)
    .pipe(sourcemaps.init())
    .pipe(sassCompiler().on('error', sassCompiler.logError))
    .pipe(postcss([postcssImport()]))
    .pipe(autoprefixer())
    .pipe(gulp.dest(cssDest))
    .pipe(sourcemaps.init())
    .pipe(sassCompiler().on('error', sassCompiler.logError))
    .pipe(postcss([postcssImport()]))
    .pipe(autoprefixer())
    .pipe(cleanCSS())
    .pipe(rename({ suffix: '.min' }))
    .pipe(gulp.dest(cssDest));
}

let myData = []
let linWas = './node_modules/**/**/*'
Object.keys(data.dependencies).map((ele)=>{
  myData.push(linWas.replace('**',ele))
})

function npmdist() {
  return [
    ...myData,
  ];
}

function copyLibsTask() {
  const destPath = 'wwwroot/assets/libs';

  return gulp.src(npmdist(), { base: './node_modules', encoding: false })
    .pipe(rename(path => {
      path.dirname = path.dirname.replace(/\/dist/, '').replace(/\\dist/, '');
    }))
    .pipe(gulp.dest(destPath));
}

// JS Minification Task (app scripts in wwwroot/js/)
function jsTask() {
  return gulp.src(['wwwroot/js/**/*.js', '!wwwroot/js/**/*.min.js'])
    .pipe(sourcemaps.init())
    .pipe(terser())
    .pipe(rename({ suffix: '.min' }))
    .pipe(sourcemaps.write('.'))
    .pipe(gulp.dest('wwwroot/js'));
}

// Build Task
const build = gulp.series(
  gulp.parallel(copyLibsTask),
  gulp.parallel(scssTask, jsTask),
);

// Default Task
const defaults = gulp.series(build, gulp.parallel(watchTask));

// Export tasks
export {
  scssTask as scss,
  jsTask as js,
  watchTask as watch,
  build,
  defaults as default
};
 
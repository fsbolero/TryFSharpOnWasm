pushd src/WebFsc.Client/wwwroot
npm install
if (test-path webfonts) { rm -r webfonts }
cp -r node_modules/@fortawesome/fontawesome-free/webfonts .
popd

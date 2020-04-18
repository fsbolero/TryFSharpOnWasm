dotnet tool restore

pushd src/WebFsc.Client
npm install
if (test-path wwwroot/webfonts) { rm -r wwwroot/webfonts }
cp -r node_modules/@fortawesome/fontawesome-free/webfonts wwwroot
popd

# pushes src/wwwroot to gh-pages branch

param ([string] $env = "local")

$msg = 'gh-pages.ps1: src/WebFsc.Client/wwwroot -> gh-pages'
$gitURL = "https://github.com/fsbolero/TryFSharpOnWasm"

write-host -foregroundColor "green" "=====> $msg"

function clearDir() {
  rm -r build/gh-pages -errorAction ignore
}

if ($env -eq "appveyor") {
  clearDir
  $d = mkdir -force build
  git clone $gitURL build/gh-pages -b gh-pages --single-branch
  cd build/gh-pages
  git config credential.helper "store --file=.git/credentials"
  $t = $env:GH_TOKEN
  $cred = "https://" + $t + ":@github.com"
  $d = pwd
  [System.IO.File]::WriteAllText("$pwd/.git/credentials", $cred)
  git config user.name "AppVeyor"
  git config user.email "websharper-support@intellifactory.com"
} else {
  clearDir
  cd build
  git clone .. gh-pages -b gh-pages --single-branch
  cd gh-pages
}

git rm -rf *
cp -r -force ../../publish/WebFsc.Client/dist/* .
echo $null >> .nojekyll
echo fsbolero.io > CNAME
(get-content '.\index.html' -encoding utf8).replace('<base href="/"', '<base href="/TryFSharpOnWasm/"') | set-content '.\index.html' -encoding utf8
git add . 2>git.log
git commit --amend -am $msg
git push -f -u origin gh-pages
cd ../..
clearDir
write-host -foregroundColor "green" "=====> DONE"

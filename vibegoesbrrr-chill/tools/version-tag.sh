#!/bin/bash
set -e
#set -o xtrace

BRANCH="$(git branch --show-current)"
REF=${1:-HEAD}

VERSION_CODE=$(grep -Po '(?<=Version = \")(\d+\.\d+.\d+)(?=\")' VibeGoesBrrrMod.cs)
VERSION_README=$(grep -Po '(?<=# Vibe Goes Brrr~ v)(\d+\.\d+.\d+)' README.md)
if [[ "${VERSION_CODE}" != "${VERSION_README}" ]]; then
  echo -e "\033[0;31mError: Code version (${VERSION_CODE}) doesn't match README version (${VERSION_README})\033[0m"
  exit 1
fi
VERSION=${VERSION_CODE}

if [[ "$BRANCH" == "master" ]]; then
  TAG="v${VERSION}"
else
  TAG="v${VERSION}-pre"
fi

# Create annotated version tag
echo -e "\033[0;36mTagging version ${TAG}\033[0m"
git tag -fa -m "VibeGoesBrrr ${TAG}" ${TAG} ${REF}
if [[ "$BRANCH" != "master" ]]; then
  git push origin :refs/tags/${TAG}
fi
git push origin refs/tags/${TAG}:refs/tags/${TAG}

# Create lightweight latest pointer
if [[ "$BRANCH" == "master" ]]; then
  echo -e "\033[0;36mUpdating latest tag\033[0m"
  git tag -f latest ${REF}
  git push origin :refs/tags/latest
  git push origin refs/tags/latest:refs/tags/latest
fi

echo -e "\033[0;36mDone!\033[0m"
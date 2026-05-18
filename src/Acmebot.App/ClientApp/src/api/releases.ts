import type { ReleaseInfo } from './types';
import { isNewerVersion, isVersionLike } from '@/utils/versions';

const releasesEndpoint = 'https://api.github.com/repos/polymind-inc/acmebot/releases?per_page=10';

interface GitHubReleaseResponse {
  tag_name?: unknown;
  html_url?: unknown;
}

export async function getLatestRelease(): Promise<ReleaseInfo | null> {
  const response = await fetch(releasesEndpoint, {
    headers: {
      Accept: 'application/vnd.github+json',
    },
  });

  if (!response.ok) {
    return null;
  }

  const releases = await response.json() as GitHubReleaseResponse[];

  return releases.reduce<ReleaseInfo | null>((latestRelease, release) => {
    if (typeof release.tag_name !== 'string' || typeof release.html_url !== 'string' || !isVersionLike(release.tag_name)) {
      return latestRelease;
    }

    const releaseInfo = {
      version: release.tag_name,
      releaseUrl: release.html_url,
    };

    if (!latestRelease || isNewerVersion(releaseInfo.version, latestRelease.version)) {
      return releaseInfo;
    }

    return latestRelease;
  }, null);
}

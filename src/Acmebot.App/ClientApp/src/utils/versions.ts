interface ParsedVersion {
  major: number;
  minor: number;
  patch: number;
  prerelease: string[];
}

const semanticVersionPattern = /^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;

export function isVersionLike(value: string): boolean {
  return parseVersion(value) !== null;
}

export function isNewerVersion(candidate: string, current: string): boolean {
  const candidateVersion = parseVersion(candidate);
  const currentVersion = parseVersion(current);

  if (!candidateVersion || !currentVersion) {
    return false;
  }

  const numericComparison = compareNumericVersion(candidateVersion, currentVersion);

  if (numericComparison !== 0) {
    return numericComparison > 0;
  }

  return comparePrerelease(candidateVersion.prerelease, currentVersion.prerelease) > 0;
}

function parseVersion(value: string): ParsedVersion | null {
  const match = semanticVersionPattern.exec(value.trim());

  if (!match) {
    return null;
  }

  const [, major, minor = '0', patch = '0', prerelease = ''] = match;

  return {
    major: Number(major),
    minor: Number(minor),
    patch: Number(patch),
    prerelease: prerelease ? prerelease.split('.') : [],
  };
}

function compareNumericVersion(candidate: ParsedVersion, current: ParsedVersion): number {
  const candidateParts = [candidate.major, candidate.minor, candidate.patch];
  const currentParts = [current.major, current.minor, current.patch];

  for (const [index, candidatePart] of candidateParts.entries()) {
    const difference = candidatePart - currentParts[index];

    if (difference !== 0) {
      return difference;
    }
  }

  return 0;
}

function comparePrerelease(candidate: string[], current: string[]): number {
  if (candidate.length === 0 && current.length > 0) {
    return 1;
  }

  if (candidate.length > 0 && current.length === 0) {
    return -1;
  }

  const length = Math.max(candidate.length, current.length);

  for (let index = 0; index < length; index += 1) {
    const candidateIdentifier = candidate[index];
    const currentIdentifier = current[index];

    if (candidateIdentifier === undefined) {
      return -1;
    }

    if (currentIdentifier === undefined) {
      return 1;
    }

    const comparison = comparePrereleaseIdentifier(candidateIdentifier, currentIdentifier);

    if (comparison !== 0) {
      return comparison;
    }
  }

  return 0;
}

function comparePrereleaseIdentifier(candidate: string, current: string): number {
  const candidateNumber = parseNumericIdentifier(candidate);
  const currentNumber = parseNumericIdentifier(current);

  if (candidateNumber !== null && currentNumber !== null) {
    return candidateNumber - currentNumber;
  }

  if (candidateNumber !== null) {
    return -1;
  }

  if (currentNumber !== null) {
    return 1;
  }

  return candidate.localeCompare(current);
}

function parseNumericIdentifier(value: string): number | null {
  if (!/^\d+$/.test(value)) {
    return null;
  }

  return Number(value);
}

import { toASCII } from 'punycode/';

import type { SelectableDnsZone } from '@/api/types';

export interface ValidationOutcome {
  value: string;
  message: string;
}

export interface CnameInstruction {
  source: string;
  target: string;
}

export function readDnsNameInput(value: string, fieldLabel: string): ValidationOutcome {
  return validateDnsName(value, fieldLabel, true);
}

export function validateOptionalDnsAlias(dnsAlias: string): ValidationOutcome {
  if (!dnsAlias.trim()) {
    return { value: '', message: '' };
  }

  return validateDnsName(dnsAlias, 'DNS Alias', false);
}

export function createManagedDnsName(recordName: string, zone: SelectableDnsZone): string {
  const zoneName = toAsciiDnsName(zone.name);
  const record = toAsciiDnsName(recordName);

  if (!record || record === '@') {
    return zoneName;
  }

  if (record === zoneName || record.endsWith(`.${zoneName}`)) {
    return record;
  }

  return `${record}.${zoneName}`;
}

export function createCertificateName(dnsName: string): string {
  return dnsName.replaceAll('*', 'wildcard').replaceAll('.', '-');
}

function validateDnsName(value: string, fieldLabel: string, allowWildcard: boolean): ValidationOutcome {
  const input = value.trim().replace(/\.+$/, '');

  if (!input) {
    return { value: '', message: `${fieldLabel} is required.` };
  }

  const asciiName = toAsciiOrNull(input);

  if (asciiName === null) {
    return { value: '', message: `${fieldLabel} contains characters that cannot be converted to a DNS name.` };
  }

  if (asciiName.length > 253) {
    return { value: '', message: `${fieldLabel} must be 253 characters or fewer.` };
  }

  const labels = asciiName.split('.');

  if (labels.length < 2) {
    return { value: '', message: `${fieldLabel} must include a domain suffix.` };
  }

  for (const [labelIndex, label] of labels.entries()) {
    if (!label) {
      return { value: '', message: `${fieldLabel} cannot contain empty DNS labels.` };
    }

    if (label.length > 63) {
      return { value: '', message: 'Each DNS label must be 63 characters or fewer.' };
    }

    if (label === '*') {
      if (!allowWildcard) {
        return { value: '', message: `${fieldLabel} cannot be a wildcard.` };
      }

      if (labelIndex !== 0) {
        return { value: '', message: 'A wildcard can only be the leftmost DNS label.' };
      }

      continue;
    }

    if (label.includes('*') || !/^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/.test(label)) {
      const message = allowWildcard
        ? `${fieldLabel} can contain only letters, numbers, hyphens, dots, and a leftmost wildcard.`
        : `${fieldLabel} can contain only letters, numbers, hyphens, and dots.`;

      return { value: '', message };
    }
  }

  return { value: asciiName, message: '' };
}

function toAsciiDnsName(value: string): string {
  const normalized = value.trim().replace(/\.+$/, '');

  try {
    return toASCII(normalized).toLowerCase();
  } catch {
    return normalized.toLowerCase();
  }
}

function toAsciiOrNull(value: string): string | null {
  try {
    return toASCII(value).toLowerCase();
  } catch {
    return null;
  }
}

export function createDelegatedDnsAlias(dnsNames: string[], zone: SelectableDnsZone): string {
  if (dnsNames.length === 0) {
    return '';
  }

  const zoneName = toAsciiDnsName(zone.name);

  if (!zoneName) {
    return '';
  }

  return `${createAliasRecordName(dnsNames)}.${zoneName}`;
}

export function createDelegatedCnameInstructions(dnsNames: string[], dnsAlias: string): CnameInstruction[] {
  if (!dnsAlias) {
    return [];
  }

  const target = `_acme-challenge.${dnsAlias}`;

  return dnsNames.map((dnsName) => ({
    source: createChallengeRecordName(dnsName),
    target,
  }));
}

function hashDnsNames(dnsNames: string[]): string {
  const value = [...dnsNames].sort((left, right) => left.localeCompare(right)).join(',');
  let hash = 0x811c9dc5;

  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }

  return hash.toString(16).padStart(8, '0');
}

function createAliasRecordName(dnsNames: string[]): string {
  const firstDnsName = dnsNames[0]?.replace(/^\*\./, 'wildcard.') ?? 'certificate';
  const hash = hashDnsNames(dnsNames);
  const stem = firstDnsName
    .toLowerCase()
    .replaceAll('*', 'wildcard')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'certificate';
  const maxStemLength = 63 - hash.length - 1;

  return `${stem.slice(0, maxStemLength).replace(/-+$/g, '') || 'certificate'}-${hash}`;
}

function createChallengeRecordName(dnsName: string): string {
  return `_acme-challenge.${dnsName.replace(/^\*\./, '')}`;
}

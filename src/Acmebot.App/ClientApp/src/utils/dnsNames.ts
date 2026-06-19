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
  const dnsName = value.trim();

  if (!dnsName) {
    return { value: '', message: `${fieldLabel} is required.` };
  }

  return { value: dnsName, message: '' };
}

export function createManagedDnsName(recordName: string, zone: SelectableDnsZone): string {
  const record = recordName.trim();
  const zoneName = zone.name.trim();

  if (!record || record === '@') {
    return zoneName;
  }

  return `${record}.${zoneName}`;
}

export function createDelegatedDnsAlias(dnsNames: string[], zone: SelectableDnsZone): string {
  if (dnsNames.length === 0) {
    return '';
  }

  const zoneName = zone.name.trim();

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

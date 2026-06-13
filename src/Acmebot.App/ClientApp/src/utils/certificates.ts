import { toASCII, toUnicode } from 'punycode/';

import type { CertificateCategory, CertificateItem, CertificateStatusKind, DnsZoneGroup } from '@/api/types';

export interface CertificateStatus {
  kind: CertificateStatusKind;
  label: string;
  remainingDays: number;
}

export function displayDnsName(value: string): string {
  try {
    return toUnicode(value);
  } catch {
    return value;
  }
}

export function getCertificateCategory(certificate: CertificateItem): CertificateCategory {
  if (!certificate.isIssuedByAcmebot) {
    return 'unmanaged';
  }

  return certificate.isSameEndpoint ? 'managed' : 'other-ca';
}

export function getCategoryLabel(category: CertificateCategory): string {
  const labels: Record<CertificateCategory, string> = {
    managed: 'Managed',
    'other-ca': 'Other CA',
    unmanaged: 'Unmanaged',
  };

  return labels[category];
}

export function getValidityDays(certificate: CertificateItem): number {
  const createdOn = new Date(certificate.createdOn).getTime();
  const expiresOn = new Date(certificate.expiresOn).getTime();

  return Math.round((expiresOn - createdOn) / 86_400_000);
}

export function isShortLived(certificate: CertificateItem): boolean {
  return getValidityDays(certificate) <= 10;
}

export function getRemainingDays(certificate: CertificateItem): number {
  const expiresOn = new Date(certificate.expiresOn).getTime();

  return Math.ceil((expiresOn - Date.now()) / 86_400_000);
}

export function getCertificateStatus(certificate: CertificateItem): CertificateStatus {
  const expiresOn = new Date(certificate.expiresOn).getTime();
  const diff = expiresOn - Date.now();
  const remainingDays = Math.round(diff / 86_400_000);
  const remainingHours = Math.round(diff / 3_600_000);

  if (!certificate.enabled) {
    return { kind: 'disabled', label: 'Disabled', remainingDays };
  }

  if (diff <= 0 || certificate.isExpired) {
    return { kind: 'expired', label: 'Expired', remainingDays };
  }

  if (isShortLived(certificate)) {
    const kind = remainingDays <= 2 ? 'warning' : 'valid';

    if (remainingHours < 24) {
      return { kind, label: `${remainingHours}h remaining`, remainingDays };
    }

    const days = Math.floor(diff / 86_400_000);
    const hours = Math.floor((diff % 86_400_000) / 3_600_000);
    return { kind, label: `${days}d ${hours}h remaining`, remainingDays };
  }

  if (remainingDays <= 30) {
    return { kind: 'warning', label: `${remainingDays}d remaining`, remainingDays };
  }

  return { kind: 'valid', label: `${remainingDays} days`, remainingDays };
}

export function formatDate(value?: string | null): string {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
  }).format(new Date(value));
}

export function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

function normalizeDnsName(value: string): string {
  const withoutWildcard = value.startsWith('*.') ? value.slice(2) : value;

  try {
    return toASCII(withoutWildcard.trim().replace(/\.+$/, '')).toLowerCase();
  } catch {
    return withoutWildcard.trim().replace(/\.+$/, '').toLowerCase();
  }
}

function findMatchingDnsZone(certificate: CertificateItem, dnsZoneGroups: DnsZoneGroup[]): string | null {
  const providerZoneNames = new Set<string>();
  const allZoneNames = new Set<string>();

  for (const group of dnsZoneGroups) {
    for (const zone of group.dnsZones ?? []) {
      const zoneName = normalizeDnsName(zone.name);

      if (!zoneName) {
        continue;
      }

      allZoneNames.add(zoneName);

      if (certificate.dnsProviderName && group.dnsProviderName === certificate.dnsProviderName) {
        providerZoneNames.add(zoneName);
      }
    }
  }

  const candidateZoneNames = (providerZoneNames.size > 0 ? Array.from(providerZoneNames) : Array.from(allZoneNames)).toSorted(
    (left, right) => left.length - right.length || left.localeCompare(right),
  );

  for (const dnsName of certificate.dnsNames) {
    const normalizedDnsName = normalizeDnsName(dnsName);
    const matchingZoneName = candidateZoneNames.find((zoneName) => normalizedDnsName === zoneName || normalizedDnsName.endsWith(`.${zoneName}`));

    if (matchingZoneName) {
      return matchingZoneName;
    }
  }

  return null;
}

export function getPrimaryZone(certificate: CertificateItem, dnsZoneGroups: DnsZoneGroup[] = []): string {
  const matchingDnsZone = findMatchingDnsZone(certificate, dnsZoneGroups);

  if (matchingDnsZone) {
    return displayDnsName(matchingDnsZone);
  }

  const firstDnsName = certificate.dnsNames[0];

  if (!firstDnsName) {
    return '(unknown)';
  }

  const normalizedName = normalizeDnsName(firstDnsName);
  const parts = normalizedName.split('.');

  if (parts.length <= 2) {
    return displayDnsName(normalizedName);
  }

  return displayDnsName(parts.slice(-2).join('.'));
}

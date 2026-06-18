import type { Component } from 'vue';
import { AlertTriangle, CalendarClock, Clock3, RotateCw } from 'lucide-vue-next';

import type { CertificateRenewalItem } from '@/api/types';
import { formatDateTime } from '@/utils/certificates';

export type RenewalTone = 'scheduled' | 'active' | 'attention' | 'pending' | 'neutral';

const knownTones: ReadonlySet<RenewalTone> = new Set(['scheduled', 'active', 'attention', 'pending']);

export function renewalTone(renewal: CertificateRenewalItem | null, loading = false): RenewalTone {
  if (!renewal) {
    return loading ? 'pending' : 'neutral';
  }

  const kind = renewal.statusKind as RenewalTone;

  return knownTones.has(kind) ? kind : 'neutral';
}

export function renewalLabel(renewal: CertificateRenewalItem | null, loading = false): string {
  if (!renewal) {
    return loading ? 'Loading' : '-';
  }

  return renewal.status;
}

export function renewalSubtext(renewal: CertificateRenewalItem | null, loading = false): string {
  if (!renewal) {
    return loading ? 'Refreshing status' : '';
  }

  if (renewal.nextCheck) {
    return `Next ${formatDateTime(renewal.nextCheck)}`;
  }

  return '';
}

export function renewalIcon(renewal: CertificateRenewalItem | null, loading = false): Component {
  const tone = renewalTone(renewal, loading);

  if (tone === 'attention') {
    return AlertTriangle;
  }

  if (tone === 'active') {
    return RotateCw;
  }

  if (tone === 'scheduled') {
    return CalendarClock;
  }

  return Clock3;
}

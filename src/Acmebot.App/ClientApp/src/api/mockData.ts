import type { CertificateItem, CertificatePolicyItem, CertificateRenewalItem, DnsZoneGroup } from './types';

const day = 86_400_000;

function dateFromNow(days: number): string {
  return new Date(Date.now() + days * day).toISOString();
}

function dateBefore(days: number): string {
  return new Date(Date.now() - days * day).toISOString();
}

let mockCertificates: CertificateItem[] = [
  {
    id: 'https://mock.vault/certificates/www-example-com',
    name: 'www-example-com',
    dnsNames: ['www.example.com', 'example.com'],
    dnsProviderName: 'Azure DNS',
    createdOn: dateBefore(38),
    expiresOn: dateFromNow(52),
    x509Thumbprint: 'A4B2C2D9F1E0ACB81E7A1022E8C5F4A55F90D4B1',
    keyType: 'RSA',
    keySize: 2048,
    reuseKey: false,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: null,
    tags: { Customer: 'Contoso', Stage: 'production' },
  },
  {
    id: 'https://mock.vault/certificates/apex-example-co-jp',
    name: 'apex-example-co-jp',
    dnsNames: ['example.co.jp'],
    dnsProviderName: 'Azure DNS',
    createdOn: dateBefore(20),
    expiresOn: dateFromNow(68),
    x509Thumbprint: '62B4E2103E9B4E46B9E09172B0A321AD108D2D54',
    keyType: 'RSA',
    keySize: 2048,
    reuseKey: false,
    enabled: false,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: null,
    tags: { Stage: 'staging' },
  },
  {
    id: 'https://mock.vault/certificates/app-www-example-co-jp',
    name: 'app-www-example-co-jp',
    dnsNames: ['app.www.example.co.jp'],
    dnsProviderName: 'Azure DNS',
    createdOn: dateBefore(19),
    expiresOn: dateFromNow(69),
    x509Thumbprint: '78E62B4E2103E9B4E46B9E09172B0A321AD108D',
    keyType: 'RSA',
    keySize: 2048,
    reuseKey: false,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: null,
  },
  {
    id: 'https://mock.vault/certificates/api-contoso-com',
    name: 'api-contoso-com',
    dnsNames: ['api.contoso.com'],
    dnsProviderName: 'Cloudflare',
    createdOn: dateBefore(82),
    expiresOn: dateFromNow(12),
    x509Thumbprint: '9C98E7D650C181B04A15AE0C096027862B73E33A',
    keyType: 'EC',
    keyCurveName: 'P-256',
    reuseKey: true,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: null,
  },
  {
    id: 'https://mock.vault/certificates/edge-adatum-io',
    name: 'edge-adatum-io',
    dnsNames: ['edge.adatum.io'],
    dnsProviderName: 'Cloudflare',
    createdOn: dateBefore(6),
    expiresOn: dateFromNow(1.5),
    x509Thumbprint: '35B9E8D3A20F42629D01C8D8CDAE0E31E2A0B90B',
    keyType: 'EC',
    keyCurveName: 'P-256',
    reuseKey: false,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: null,
  },
  {
    id: 'https://mock.vault/certificates/wildcard-fabrikam-net',
    name: 'wildcard-fabrikam-net',
    dnsNames: ['*.fabrikam.net', 'fabrikam.net'],
    dnsProviderName: 'Azure DNS',
    createdOn: dateBefore(94),
    expiresOn: dateFromNow(-3),
    x509Thumbprint: '10F0403D238C285E9B4E46B9E09172B0A321AD10',
    keyType: 'RSA',
    keySize: 3072,
    reuseKey: false,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: true,
    acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
    dnsAlias: 'dns-alias.fabrikam.net',
    profile: 'tlsserver',
  },
  {
    id: 'https://mock.vault/certificates/portal-example-org',
    name: 'portal-example-org',
    dnsNames: ['portal.example.org'],
    dnsProviderName: 'Azure DNS',
    createdOn: dateBefore(31),
    expiresOn: dateFromNow(58),
    x509Thumbprint: '66342F778F40DA3CCECFB2D908390F0B4119084A',
    keyType: 'RSA',
    keySize: 2048,
    reuseKey: false,
    enabled: true,
    isIssuedByAcmebot: true,
    isSameEndpoint: false,
    acmeEndpoint: 'https://acme.zerossl.com/v2/DV90',
    dnsAlias: null,
  },
  {
    id: 'https://mock.vault/certificates/imported-legacy-net',
    name: 'imported-legacy-net',
    dnsNames: ['legacy.net'],
    dnsProviderName: '',
    createdOn: dateBefore(120),
    expiresOn: dateFromNow(144),
    x509Thumbprint: 'E42F40A663778F40DA3CCECFB2D908390F0B4119',
    keyType: 'RSA',
    keySize: 4096,
    reuseKey: null,
    enabled: true,
    isIssuedByAcmebot: false,
    isSameEndpoint: false,
    acmeEndpoint: null,
    dnsAlias: null,
  },
];

const mockDnsZoneGroups: DnsZoneGroup[] = [
  {
    dnsProviderName: 'Azure DNS',
    dnsZones: [{ name: 'example.com' }, { name: 'acme.example.com' }, { name: 'example.co.jp' }, { name: 'www.example.co.jp' }, { name: 'example.org' }, { name: 'fabrikam.net' }],
  },
  {
    dnsProviderName: 'Cloudflare',
    dnsZones: [{ name: 'contoso.com' }, { name: 'adatum.io' }],
  },
  {
    dnsProviderName: 'Route 53',
    dnsZones: [{ name: 'wingtiptoys.com' }],
  },
];

const mockRenewalSchedules: Record<string, Partial<CertificateRenewalItem>> = {
  'edge-adatum-io': {
    status: 'Renewing',
    statusKind: 'active',
    message: 'Certificate is within the renewal window.',
    lastCheckedAt: dateBefore(0.02),
  },
  'api-contoso-com': {
    status: 'Scheduled',
    statusKind: 'scheduled',
    message: 'Certificate expires in 12 days.',
    nextCheck: dateFromNow(1.5),
    lastCheckedAt: dateBefore(0.3),
  },
  'www-example-com': {
    status: 'Scheduled',
    statusKind: 'scheduled',
    message: 'Certificate is healthy.',
    nextCheck: dateFromNow(24),
    lastCheckedAt: dateBefore(1),
  },
  'wildcard-fabrikam-net': {
    status: 'Retrying',
    statusKind: 'attention',
    message: 'Automatic renewal failed. Retrying later.',
    nextCheck: dateFromNow(0.25),
    lastCheckedAt: dateBefore(0.1),
  },
};

export async function getMockCertificates(): Promise<CertificateItem[]> {
  await delay(250);
  return structuredClone(mockCertificates).toSorted((left, right) => left.expiresOn.localeCompare(right.expiresOn));
}

export async function getMockDnsZones(): Promise<DnsZoneGroup[]> {
  await delay(250);
  return structuredClone(mockDnsZoneGroups);
}

export async function getMockCertificateRenewals(): Promise<CertificateRenewalItem[]> {
  await delay(250);
  return structuredClone(mockCertificates.map(createMockCertificateRenewal)).toSorted(compareCertificateRenewals);
}

export async function mockIssueCertificate(policy: CertificatePolicyItem): Promise<void> {
  await delay(700);

  mockCertificates = [
    ...mockCertificates,
    {
      id: `https://mock.vault/certificates/${policy.certificateName}`,
      name: policy.certificateName,
      dnsNames: [...policy.dnsNames],
      dnsProviderName: policy.dnsProviderName,
      createdOn: new Date().toISOString(),
      expiresOn: dateFromNow(90),
      x509Thumbprint: 'MOCKEDCERTIFICATEOPERATION0000000000000000',
      keyType: policy.keyType,
      keySize: policy.keySize,
      keyCurveName: policy.keyCurveName,
      reuseKey: policy.reuseKey,
      enabled: true,
      isIssuedByAcmebot: true,
      isSameEndpoint: true,
      acmeEndpoint: 'https://acme-v02.api.letsencrypt.org/directory',
      dnsAlias: policy.dnsAlias,
      tags: policy.tags ? { ...policy.tags } : {},
    },
  ];
}

export async function mockRenewCertificate(certificateName: string): Promise<void> {
  await delay(650);
  mockCertificates = mockCertificates.map((certificate) =>
    certificate.name === certificateName
      ? {
          ...certificate,
          createdOn: new Date().toISOString(),
          expiresOn: dateFromNow(90),
          enabled: true,
        }
      : certificate,
  );
}

export async function mockRevokeCertificate(certificateName: string): Promise<void> {
  await delay(650);
  mockCertificates = mockCertificates.map((certificate) => (certificate.name === certificateName ? { ...certificate, enabled: false } : certificate));
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

function createMockCertificateRenewal(certificate: CertificateItem): CertificateRenewalItem {
  const schedule = mockRenewalSchedules[certificate.name];
  const base = {
    certificateName: certificate.name,
  };

  if (!certificate.enabled) {
    return {
      ...base,
      status: 'Disabled',
      statusKind: 'disabled',
      message: 'Automatic renewal is paused because this certificate is disabled.',
      nextCheck: null,
      lastCheckedAt: null,
    };
  }

  if (!certificate.isIssuedByAcmebot || !certificate.isSameEndpoint) {
    return {
      ...base,
      status: 'Not managed',
      statusKind: 'neutral',
      message: 'This certificate is not managed by this Acmebot endpoint.',
      nextCheck: null,
      lastCheckedAt: null,
    };
  }

  if (!schedule) {
    return {
      ...base,
      status: 'Not scheduled',
      statusKind: 'pending',
      message: 'Automatic renewal will start after the daily renewal check runs.',
      nextCheck: null,
      lastCheckedAt: null,
    };
  }

  return {
    ...base,
    status: schedule.status ?? 'Checking',
    statusKind: schedule.statusKind ?? 'pending',
    message: schedule.message ?? 'Automatic renewal status is being refreshed.',
    nextCheck: schedule.nextCheck ?? null,
    lastCheckedAt: schedule.lastCheckedAt ?? null,
  };
}

function compareCertificateRenewals(left: CertificateRenewalItem, right: CertificateRenewalItem): number {
  return (
    getCertificateRenewalSortRank(left) - getCertificateRenewalSortRank(right) ||
    (left.nextCheck ?? '9999-12-31T23:59:59.999Z').localeCompare(right.nextCheck ?? '9999-12-31T23:59:59.999Z') ||
    left.certificateName.localeCompare(right.certificateName)
  );
}

function getCertificateRenewalSortRank(renewal: CertificateRenewalItem): number {
  const ranks: Record<string, number> = {
    attention: 0,
    active: 1,
    pending: 2,
    scheduled: 3,
    disabled: 4,
    neutral: 5,
  };

  return ranks[renewal.statusKind] ?? 5;
}

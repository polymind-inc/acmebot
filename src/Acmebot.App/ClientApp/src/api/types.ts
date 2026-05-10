export type KeyType = 'RSA' | 'EC';
export type KeyCurveName = 'P-256' | 'P-384' | 'P-521' | 'P-256K';
export type CertificateCategory = 'managed' | 'other-ca' | 'unmanaged';
export type CertificateStatusKind = 'valid' | 'warning' | 'expired';

export interface CertificateItem {
  id: string;
  name: string;
  dnsNames: string[];
  dnsProviderName?: string | null;
  createdOn: string;
  expiresOn: string;
  x509Thumbprint?: string | null;
  keyType?: KeyType | string | null;
  keySize?: number | null;
  keyCurveName?: KeyCurveName | string | null;
  reuseKey?: boolean | null;
  isExpired: boolean;
  isIssuedByAcmebot: boolean;
  isSameEndpoint: boolean;
  acmeEndpoint?: string | null;
  dnsAlias?: string | null;
  tags?: Record<string, string> | null;
}

export interface DnsZoneItem {
  name: string;
}

export interface DnsZoneGroup {
  dnsProviderName: string;
  dnsZones: DnsZoneItem[] | null;
}

export interface SelectableDnsZone extends DnsZoneItem {
  dnsProviderName: string;
}

export interface CertificatePolicyItem {
  certificateName?: string;
  dnsNames: string[];
  dnsProviderName?: string;
  keyType: KeyType;
  keySize?: number;
  keyCurveName?: KeyCurveName;
  reuseKey?: boolean;
  dnsAlias?: string;
  tags?: Record<string, string>;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  output?: string;
  errors?: Record<string, string[]>;
}

import type { CertificateItem, CertificatePolicyItem, CertificateRenewalItem, DnsZoneGroup, ProblemDetails } from './types';

const useMockApi = import.meta.env.DEV && import.meta.env.VITE_ACMEBOT_USE_MOCKS === 'true';

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const text = await response.text();

  if (!text) {
    return undefined;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function getProblemMessage(status: number, body: unknown): string {
  if (typeof body === 'string') {
    return body;
  }

  const problem = body as ProblemDetails | undefined;

  if (problem?.errors) {
    return Object.values(problem.errors).flat().join('\n');
  }

  return problem?.detail ?? problem?.output ?? problem?.title ?? `HTTP Response ${status} Error`;
}

async function requestJson<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
  const response = await fetch(input, {
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    ...init,
  });
  const body = await parseResponseBody(response);

  if (!response.ok) {
    throw new ApiError(getProblemMessage(response.status, body), response.status, body as ProblemDetails);
  }

  return body as T;
}

async function startOperation(input: RequestInfo | URL, init?: RequestInit): Promise<string> {
  const response = await fetch(input, {
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    ...init,
  });
  const body = await parseResponseBody(response);

  if (!response.ok) {
    throw new ApiError(getProblemMessage(response.status, body), response.status, body as ProblemDetails);
  }

  const location = response.headers.get('location');

  if (!location) {
    throw new ApiError('The operation response did not include a status URL.', response.status);
  }

  return location;
}

async function pollOperation(location: string): Promise<void> {
  while (true) {
    await new Promise((resolve) => window.setTimeout(resolve, 3000));

    const response = await fetch(location, { headers: { Accept: 'application/json' } });
    const body = await parseResponseBody(response);

    if (response.status === 200) {
      return;
    }

    if (response.status !== 202) {
      throw new ApiError(getProblemMessage(response.status, body), response.status, body as ProblemDetails);
    }
  }
}

export async function getCertificates(): Promise<CertificateItem[]> {
  if (useMockApi) {
    const { getMockCertificates } = await import('./mockData');
    return getMockCertificates();
  }

  const certificates = await requestJson<CertificateItem[]>('/api/certificates');

  return certificates.toSorted((left, right) => left.expiresOn.localeCompare(right.expiresOn));
}

export async function getDnsZones(): Promise<DnsZoneGroup[]> {
  if (useMockApi) {
    const { getMockDnsZones } = await import('./mockData');
    return getMockDnsZones();
  }

  return requestJson<DnsZoneGroup[]>('/api/dns-zones');
}

export async function getCertificateRenewals(): Promise<CertificateRenewalItem[]> {
  if (useMockApi) {
    const { getMockCertificateRenewals } = await import('./mockData');
    return getMockCertificateRenewals();
  }

  return requestJson<CertificateRenewalItem[]>('/api/renewals');
}

export async function issueCertificate(policy: CertificatePolicyItem): Promise<void> {
  if (useMockApi) {
    const { mockIssueCertificate } = await import('./mockData');
    await mockIssueCertificate(policy);
    return;
  }

  const location = await startOperation('/api/certificates', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(policy),
  });

  await pollOperation(location);
}

export async function renewCertificate(certificateName: string): Promise<void> {
  if (useMockApi) {
    const { mockRenewCertificate } = await import('./mockData');
    await mockRenewCertificate(certificateName);
    return;
  }

  const location = await startOperation(`/api/certificates/${encodeURIComponent(certificateName)}/renew`, {
    method: 'POST',
  });

  await pollOperation(location);
}

export async function revokeCertificate(certificateName: string): Promise<void> {
  if (useMockApi) {
    const { mockRevokeCertificate } = await import('./mockData');
    await mockRevokeCertificate(certificateName);
    return;
  }

  await requestJson(`/api/certificates/${encodeURIComponent(certificateName)}/revoke`, {
    method: 'POST',
  });
}

export function formatApiError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'An unexpected error occurred.';
}

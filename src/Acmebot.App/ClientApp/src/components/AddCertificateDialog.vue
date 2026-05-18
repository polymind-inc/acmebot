<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { CirclePlus, KeyRound, Plus, ShieldPlus, Tag, Trash2, X } from 'lucide-vue-next';
import { toASCII } from 'punycode/';

import type { CertificatePolicyItem, DnsZoneGroup, KeyCurveName, KeyType, SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';

import SearchableZoneSelect from './SearchableZoneSelect.vue';

const props = defineProps<{
  open: boolean;
  zones: DnsZoneGroup[];
  loadingZones: boolean;
  sending: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [policy: CertificatePolicyItem];
  'load-zones': [];
}>();

interface ValidationOutcome {
  value: string;
  message: string;
}

interface CertificateTagInput {
  id: number;
  key: string;
  value: string;
}

interface TagValidationOutcome {
  tags: Record<string, string>;
  message: string;
}

const selectedZone = ref<SelectableDnsZone | null>(null);
const validationErrors = reactive({
  dnsName: '',
});
let certificateTagId = 0;

const validKeySizes = [2048, 3072, 4096];
const validKeyCurves: KeyCurveName[] = ['P-256', 'P-384', 'P-521', 'P-256K'];

const form = reactive({
  recordName: '',
  dnsNames: [] as string[],
  dnsProviderName: '',
  useAdvancedOptions: false,
  certificateName: '',
  keyType: 'RSA' as KeyType,
  keySize: 2048,
  keyCurveName: 'P-256' as KeyCurveName,
  reuseKey: false,
  dnsAlias: '',
  tags: [] as CertificateTagInput[],
});

const certificateNameError = computed(() => (form.useAdvancedOptions ? validateCertificateName(form.certificateName) : ''));
const dnsAliasValidation = computed(() => (form.useAdvancedOptions ? validateOptionalDnsAlias(form.dnsAlias) : { value: '', message: '' }));
const dnsAliasError = computed(() => dnsAliasValidation.value.message);
const keyOptionError = computed(() => validateKeyOptions());
const tagValidation = computed(() => (form.useAdvancedOptions ? validateTags(form.tags) : { tags: {}, message: '' }));
const tagError = computed(() => tagValidation.value.message);
const submitValidationMessage = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'Add at least one DNS name.';
  }

  return certificateNameError.value || dnsAliasError.value || keyOptionError.value || tagError.value;
});
const canSubmit = computed(() => !props.sending && submitValidationMessage.value === '');
const fullDnsName = computed(() => (selectedZone.value ? validateRecordDnsName(form.recordName, selectedZone.value).value || null : null));
const keySummary = computed(() => (form.keyType === 'RSA' ? `${form.keySize} bit RSA` : `${form.keyCurveName} EC`));
const issueStatusLabel = computed(() => {
  if (form.dnsNames.length > 0) {
    return 'Ready';
  }

  if (selectedZone.value) {
    return 'Add DNS name';
  }

  return 'Select zone';
});
const dnsNamesSummary = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'None added';
  }

  const firstDnsName = displayDnsName(form.dnsNames[0]);

  if (form.dnsNames.length === 1) {
    return firstDnsName;
  }

  return `${firstDnsName} +${form.dnsNames.length - 1}`;
});
const dnsNameCountLabel = computed(() => `${form.dnsNames.length} ${form.dnsNames.length === 1 ? 'DNS name' : 'DNS names'}`);
const tagCount = computed(() => Object.keys(tagValidation.value.tags).length);
const tagCountLabel = computed(() => (tagCount.value === 0 ? 'No tags' : `${tagCount.value} ${tagCount.value === 1 ? 'tag' : 'tags'}`));

watch(
  () => props.open,
  (open) => {
    if (open) {
      resetForm();
      emit('load-zones');
    }
  },
);

watch(
  () => form.useAdvancedOptions,
  (useAdvancedOptions) => {
    if (!useAdvancedOptions) {
      form.certificateName = '';
      form.keyType = 'RSA';
      form.keySize = 2048;
      form.keyCurveName = 'P-256';
      form.reuseKey = false;
      form.dnsAlias = '';
      form.tags = [];
    }
  },
);

function resetForm(): void {
  selectedZone.value = null;
  validationErrors.dnsName = '';
  form.recordName = '';
  form.dnsNames = [];
  form.dnsProviderName = '';
  form.useAdvancedOptions = false;
  form.certificateName = '';
  form.keyType = 'RSA';
  form.keySize = 2048;
  form.keyCurveName = 'P-256';
  form.reuseKey = false;
  form.dnsAlias = '';
  form.tags = [];
}

function createTagInput(): CertificateTagInput {
  return { id: ++certificateTagId, key: '', value: '' };
}

function normalizeRecordName(recordName: string): string {
  return recordName.trim().replace(/\.+$/, '');
}

function toAsciiDnsName(value: string): string | null {
  try {
    return toASCII(value).toLowerCase();
  } catch {
    return null;
  }
}

function validateCertificateName(certificateName: string): string {
  const normalizedCertificateName = certificateName.trim();

  if (!normalizedCertificateName) {
    return '';
  }

  if (normalizedCertificateName.length > 127) {
    return 'Certificate Name must be 127 characters or fewer.';
  }

  if (!/^[0-9a-zA-Z-]+$/.test(normalizedCertificateName)) {
    return 'Certificate Name can contain only letters, numbers, and hyphens.';
  }

  return '';
}

function validateDnsName(value: string, fieldLabel: string, allowWildcard: boolean): ValidationOutcome {
  const normalizedInput = normalizeRecordName(value);

  if (!normalizedInput) {
    return { value: '', message: `${fieldLabel} is required.` };
  }

  const asciiName = toAsciiDnsName(normalizedInput);

  if (!asciiName) {
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
      return { value: '', message: `${fieldLabel} can contain only letters, numbers, hyphens, dots, and a leftmost wildcard.` };
    }
  }

  return { value: asciiName, message: '' };
}

function validateOptionalDnsAlias(dnsAlias: string): ValidationOutcome {
  if (!dnsAlias.trim()) {
    return { value: '', message: '' };
  }

  return validateDnsName(dnsAlias, 'DNS Alias', false);
}

function validateRecordDnsName(recordName: string, zone: SelectableDnsZone): ValidationOutcome {
  const zoneValidation = validateDnsName(zone.name, 'DNS zone', false);

  if (zoneValidation.message) {
    return zoneValidation;
  }

  const normalizedRecordName = normalizeRecordName(recordName);
  let candidateDnsName: string;

  if (!normalizedRecordName || normalizedRecordName === '@') {
    candidateDnsName = zoneValidation.value;
  } else {
    const asciiRecordName = toAsciiDnsName(normalizedRecordName);

    if (!asciiRecordName) {
      return { value: '', message: 'DNS Name contains characters that cannot be converted to a DNS name.' };
    }

    if (asciiRecordName === zoneValidation.value) {
      candidateDnsName = zoneValidation.value;
    } else if (asciiRecordName.endsWith(`.${zoneValidation.value}`)) {
      candidateDnsName = asciiRecordName;
    } else {
      candidateDnsName = `${asciiRecordName}.${zoneValidation.value}`;
    }
  }

  return validateDnsName(candidateDnsName, 'DNS Name', true);
}

function validateKeyOptions(): string {
  if (form.keyType === 'RSA' && !validKeySizes.includes(form.keySize)) {
    return 'Key Size must be 2048, 3072, or 4096 when Key Type is RSA.';
  }

  if (form.keyType === 'EC' && !validKeyCurves.includes(form.keyCurveName)) {
    return 'Curve must be P-256, P-384, P-521, or P-256K when Key Type is EC.';
  }

  return '';
}

function validateTags(items: CertificateTagInput[]): TagValidationOutcome {
  const tags: Record<string, string> = {};
  const seenKeys = new Set<string>();

  for (const item of items) {
    const key = item.key.trim();
    const value = item.value.trim();

    if (!key && !value) {
      continue;
    }

    if (!key) {
      return { tags: {}, message: 'Tag name is required.' };
    }

    if (key.toLowerCase() === 'acmebot') {
      return { tags: {}, message: 'The Acmebot tag is managed by Acmebot.' };
    }

    const normalizedKey = key.toLowerCase();

    if (seenKeys.has(normalizedKey)) {
      return { tags: {}, message: 'Tag names must be unique.' };
    }

    seenKeys.add(normalizedKey);
    tags[key] = value;
  }

  return { tags, message: '' };
}

function addTag(): void {
  if (form.tags.some((tag) => !tag.key.trim() && !tag.value.trim())) {
    return;
  }

  form.tags.push(createTagInput());
}

function removeTag(id: number): void {
  form.tags = form.tags.filter((tag) => tag.id !== id);
}

function clearDnsNameError(): void {
  validationErrors.dnsName = '';
}

function addDnsName(): void {
  validationErrors.dnsName = '';

  if (!selectedZone.value) {
    validationErrors.dnsName = 'Select a DNS zone before adding a DNS name.';
    return;
  }

  if (form.dnsProviderName && form.dnsProviderName !== selectedZone.value.dnsProviderName) {
    validationErrors.dnsName = 'DNS names in one certificate must use the same DNS provider.';
    return;
  }

  const dnsNameValidation = validateRecordDnsName(form.recordName, selectedZone.value);

  if (dnsNameValidation.message) {
    validationErrors.dnsName = dnsNameValidation.message;
    return;
  }

  if (form.dnsNames.some((dnsName) => dnsName.toLowerCase() === dnsNameValidation.value)) {
    validationErrors.dnsName = 'This DNS name is already in the certificate.';
    return;
  }

  form.dnsNames.push(dnsNameValidation.value);
  form.dnsProviderName = selectedZone.value.dnsProviderName;
  form.recordName = '';
}

function removeDnsName(dnsName: string): void {
  form.dnsNames = form.dnsNames.filter((candidate) => candidate !== dnsName);

  if (form.dnsNames.length === 0) {
    form.dnsProviderName = '';
    validationErrors.dnsName = '';
  }
}

function submit(): void {
  if (form.dnsNames.length === 0) {
    validationErrors.dnsName = 'Add at least one DNS name before issuing the certificate.';
    return;
  }

  if (!canSubmit.value) {
    return;
  }

  const normalizedCertificateName = form.useAdvancedOptions ? form.certificateName.trim() : '';
  const normalizedDnsAlias = form.useAdvancedOptions ? dnsAliasValidation.value.value : '';

  const policy: CertificatePolicyItem = {
    dnsNames: form.dnsNames,
    dnsProviderName: form.dnsProviderName || undefined,
    certificateName: normalizedCertificateName || undefined,
    keyType: form.keyType,
    reuseKey: form.useAdvancedOptions ? form.reuseKey : false,
    dnsAlias: normalizedDnsAlias || undefined,
  };

  if (form.keyType === 'RSA') {
    policy.keySize = form.keySize;
  } else {
    policy.keyCurveName = form.keyCurveName;
  }

  if (form.useAdvancedOptions && tagCount.value > 0) {
    policy.tags = tagValidation.value.tags;
  }

  emit('submit', policy);
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="modal-shell"
      role="dialog"
      aria-modal="true"
      aria-labelledby="add-certificate-heading"
    >
      <button
        class="modal-scrim"
        type="button"
        title="Close issue certificate"
        :disabled="sending"
        @click="emit('close')"
      />
      <section class="modal-panel modal-panel--wide">
        <header class="modal-panel__header">
          <div>
            <div class="eyebrow">
              Certificate issuance
            </div>
            <h2 id="add-certificate-heading">
              Issue Certificate
            </h2>
          </div>
          <button
            class="icon-only-button"
            type="button"
            title="Close issue certificate"
            :disabled="sending"
            @click="emit('close')"
          >
            <X
              :size="18"
              aria-hidden="true"
            />
          </button>
        </header>

        <div class="wizard-layout">
          <aside
            class="setup-rail"
            aria-label="Certificate issue setup"
          >
            <div class="setup-rail__header">
              <span>Issue setup</span>
              <strong>{{ issueStatusLabel }}</strong>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': selectedZone }"
            >
              <ShieldPlus
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Zone</span>
                <strong>{{ selectedZone ? displayDnsName(selectedZone.name) : 'Not selected' }}</strong>
                <small v-if="selectedZone">{{ selectedZone.dnsProviderName }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': form.dnsNames.length > 0 }"
            >
              <CirclePlus
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Names</span>
                <strong>{{ dnsNamesSummary }}</strong>
                <small>{{ dnsNameCountLabel }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': form.useAdvancedOptions }"
            >
              <KeyRound
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Key</span>
                <strong>{{ keySummary }}</strong>
                <small>{{ form.useAdvancedOptions ? 'Custom settings' : 'Default settings' }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': tagCount > 0 }"
            >
              <Tag
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Tags</span>
                <strong>{{ tagCountLabel }}</strong>
                <small>Key Vault</small>
              </div>
            </div>
          </aside>

          <div class="wizard-body">
            <div class="form-section">
              <label class="form-label">DNS Zone</label>
              <SearchableZoneSelect
                v-model:selected="selectedZone"
                :groups="zones"
                :loading="loadingZones"
              />
            </div>

            <div class="form-section">
              <label
                class="form-label"
                for="record-name"
              >DNS Name</label>
              <div class="compound-input">
                <input
                  id="record-name"
                  v-model="form.recordName"
                  type="text"
                  placeholder="@, www, api, *"
                  :disabled="!selectedZone"
                  :aria-invalid="validationErrors.dnsName ? 'true' : 'false'"
                  @keydown.enter.prevent="addDnsName"
                  @input="clearDnsNameError"
                >
                <span class="compound-input__suffix">.{{ selectedZone ? displayDnsName(selectedZone.name) : 'zone' }}</span>
                <button
                  class="icon-button"
                  type="button"
                  :disabled="!selectedZone"
                  @click="addDnsName"
                >
                  <Plus
                    :size="16"
                    aria-hidden="true"
                  />
                  <span>Add</span>
                </button>
              </div>
              <div
                v-if="fullDnsName"
                class="form-result"
              >
                <span>Full DNS name</span>
                <strong>{{ displayDnsName(fullDnsName) }}</strong>
              </div>
              <p
                v-if="validationErrors.dnsName"
                class="form-error"
              >
                {{ validationErrors.dnsName }}
              </p>
              <div class="dns-list dns-list--editable">
                <span
                  v-for="dnsName in form.dnsNames"
                  :key="dnsName"
                  class="dns-chip dns-chip--removable"
                >
                  {{ displayDnsName(dnsName) }}
                  <button
                    type="button"
                    title="Remove DNS name"
                    @click="removeDnsName(dnsName)"
                  >
                    <Trash2
                      :size="13"
                      aria-hidden="true"
                    />
                  </button>
                </span>
              </div>
            </div>

            <div class="form-section form-section--inline">
              <label class="toggle-row">
                <input
                  v-model="form.useAdvancedOptions"
                  type="checkbox"
                >
                <span>Advanced options</span>
              </label>
            </div>

            <div
              v-if="form.useAdvancedOptions"
              class="advanced-grid"
            >
              <label
                class="form-field"
                :class="{ 'is-invalid': certificateNameError }"
              >
                <span class="form-label">Certificate Name</span>
                <input
                  v-model="form.certificateName"
                  type="text"
                  placeholder="Optional certificate name"
                  :aria-invalid="certificateNameError ? 'true' : 'false'"
                >
                <span
                  v-if="certificateNameError"
                  class="form-error"
                >{{ certificateNameError }}</span>
              </label>

              <label
                class="form-field"
                :class="{ 'is-invalid': keyOptionError }"
              >
                <span class="form-label">Key Type</span>
                <select v-model="form.keyType">
                  <option value="RSA">RSA</option>
                  <option value="EC">EC</option>
                </select>
                <span
                  v-if="keyOptionError"
                  class="form-error"
                >{{ keyOptionError }}</span>
              </label>

              <label
                v-if="form.keyType === 'RSA'"
                class="form-field"
              >
                <span class="form-label">Key Size</span>
                <select v-model.number="form.keySize">
                  <option :value="2048">2048</option>
                  <option :value="3072">3072</option>
                  <option :value="4096">4096</option>
                </select>
              </label>

              <label
                v-else
                class="form-field"
              >
                <span class="form-label">Curve</span>
                <select v-model="form.keyCurveName">
                  <option value="P-256">P-256</option>
                  <option value="P-384">P-384</option>
                  <option value="P-521">P-521</option>
                  <option value="P-256K">P-256K</option>
                </select>
              </label>

              <label
                class="form-field"
                :class="{ 'is-invalid': dnsAliasError }"
              >
                <span class="form-label">DNS Alias</span>
                <input
                  v-model="form.dnsAlias"
                  type="text"
                  placeholder="alias.example.com"
                  :aria-invalid="dnsAliasError ? 'true' : 'false'"
                >
                <span
                  v-if="dnsAliasError"
                  class="form-error"
                >{{ dnsAliasError }}</span>
              </label>

              <label class="toggle-row advanced-grid__toggle">
                <input
                  v-model="form.reuseKey"
                  type="checkbox"
                >
                <span>Reuse key on renewal</span>
              </label>

              <div class="tag-editor advanced-grid__wide">
                <div class="tag-editor__header">
                  <span class="form-label">Key Vault Tags</span>
                  <button
                    class="secondary-button"
                    type="button"
                    @click="addTag"
                  >
                    <Plus
                      :size="16"
                      aria-hidden="true"
                    />
                    <span>Add tag</span>
                  </button>
                </div>
                <div
                  v-if="form.tags.length === 0"
                  class="tag-editor__empty"
                >
                  No tags
                </div>
                <div
                  v-else
                  class="tag-editor__rows"
                >
                  <div
                    v-for="tagItem in form.tags"
                    :key="tagItem.id"
                    class="tag-row"
                  >
                    <label
                      class="visually-hidden"
                      :for="`tag-key-${tagItem.id}`"
                    >Tag name</label>
                    <input
                      :id="`tag-key-${tagItem.id}`"
                      v-model="tagItem.key"
                      type="text"
                      placeholder="Name"
                      :aria-invalid="tagError ? 'true' : 'false'"
                    >
                    <label
                      class="visually-hidden"
                      :for="`tag-value-${tagItem.id}`"
                    >Tag value</label>
                    <input
                      :id="`tag-value-${tagItem.id}`"
                      v-model="tagItem.value"
                      type="text"
                      placeholder="Value"
                      :aria-invalid="tagError ? 'true' : 'false'"
                    >
                    <button
                      class="icon-only-button"
                      type="button"
                      title="Remove tag"
                      @click="removeTag(tagItem.id)"
                    >
                      <Trash2
                        :size="15"
                        aria-hidden="true"
                      />
                    </button>
                  </div>
                </div>
                <span
                  v-if="tagError"
                  class="form-error"
                >{{ tagError }}</span>
              </div>
            </div>
          </div>
        </div>

        <footer class="modal-panel__footer">
          <div class="modal-panel__footer-meta">
            <span>{{ dnsNameCountLabel }}</span>
            <span>{{ keySummary }}</span>
            <span>{{ tagCountLabel }}</span>
          </div>
          <div class="modal-panel__footer-actions">
            <button
              class="secondary-button"
              type="button"
              :disabled="sending"
              @click="emit('close')"
            >
              Cancel
            </button>
            <button
              class="primary-button"
              type="button"
              :disabled="!canSubmit"
              @click="submit"
            >
              <ShieldPlus
                :size="17"
                aria-hidden="true"
              />
              <span>Issue Certificate</span>
            </button>
          </div>
        </footer>
      </section>
    </div>
  </Teleport>
</template>

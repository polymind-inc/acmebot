<script setup lang="ts">
import { Plus, Trash2 } from 'lucide-vue-next';

import type { KeyCurveName, KeyType } from '@/api/types';

interface CertificateTagInput {
  id: number;
  key: string;
  value: string;
}

const props = defineProps<{
  certificateName: string;
  certificateNameError: string;
  keyType: KeyType;
  keyOptionError: string;
  keySize: number;
  keyCurveName: KeyCurveName;
  reuseKey: boolean;
  profile: string;
  tags: CertificateTagInput[];
  tagError: string;
}>();

const emit = defineEmits<{
  'update:certificateName': [value: string];
  'update:keyType': [value: KeyType];
  'update:keySize': [value: number];
  'update:keyCurveName': [value: KeyCurveName];
  'update:reuseKey': [value: boolean];
  'update:profile': [value: string];
  'update:tags': [value: CertificateTagInput[]];
}>();

function readFieldValue(event: Event): string {
  const target = event.target;

  if (target instanceof HTMLInputElement || target instanceof HTMLSelectElement) {
    return target.value;
  }

  return '';
}

function nextTagId(): number {
  return Math.max(0, ...props.tags.map((tag) => tag.id)) + 1;
}

function addTag(): void {
  if (props.tags.some((tag) => !tag.key.trim() && !tag.value.trim())) {
    return;
  }

  emit('update:tags', [...props.tags, { id: nextTagId(), key: '', value: '' }]);
}

function removeTag(id: number): void {
  emit('update:tags', props.tags.filter((tag) => tag.id !== id));
}

function updateTagKey(id: number, event: Event): void {
  const key = readFieldValue(event);

  emit('update:tags', props.tags.map((tag) => (tag.id === id ? { ...tag, key } : tag)));
}

function updateTagValue(id: number, event: Event): void {
  const value = readFieldValue(event);

  emit('update:tags', props.tags.map((tag) => (tag.id === id ? { ...tag, value } : tag)));
}

function updateKeyType(event: Event): void {
  emit('update:keyType', readFieldValue(event) as KeyType);
}

function updateKeySize(event: Event): void {
  emit('update:keySize', Number(readFieldValue(event)));
}

function updateKeyCurveName(event: Event): void {
  emit('update:keyCurveName', readFieldValue(event) as KeyCurveName);
}

function updateReuseKey(event: Event): void {
  emit('update:reuseKey', readFieldValue(event) === 'true');
}
</script>

<template>
  <div class="advanced-grid">
    <label
      class="form-field"
      :class="{ 'is-invalid': certificateNameError }"
    >
      <span class="form-label">Certificate Name</span>
      <input
        :value="certificateName"
        type="text"
        placeholder="Defaults to first DNS name"
        :aria-invalid="certificateNameError ? 'true' : 'false'"
        @input="emit('update:certificateName', readFieldValue($event))"
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
      <select
        :value="keyType"
        @change="updateKeyType"
      >
        <option value="RSA">RSA</option>
        <option value="EC">EC</option>
      </select>
      <span
        v-if="keyOptionError"
        class="form-error"
      >{{ keyOptionError }}</span>
    </label>

    <label
      v-if="keyType === 'RSA'"
      class="form-field"
    >
      <span class="form-label">Key Size</span>
      <select
        :value="keySize"
        @change="updateKeySize"
      >
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
      <select
        :value="keyCurveName"
        @change="updateKeyCurveName"
      >
        <option value="P-256">P-256</option>
        <option value="P-384">P-384</option>
        <option value="P-521">P-521</option>
        <option value="P-256K">P-256K</option>
      </select>
    </label>

    <label class="form-field">
      <span class="form-label">Reuse key on renewal</span>
      <select
        :value="String(reuseKey)"
        @change="updateReuseKey"
      >
        <option value="false">No</option>
        <option value="true">Yes</option>
      </select>
    </label>

    <label class="form-field">
      <span class="form-label">ACME Profile</span>
      <input
        :value="profile"
        type="text"
        placeholder="Deployment default"
        @input="emit('update:profile', readFieldValue($event))"
      >
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
        v-if="tags.length === 0"
        class="tag-editor__empty"
      >
        No tags
      </div>
      <div
        v-else
        class="tag-editor__rows"
      >
        <div
          v-for="tagItem in tags"
          :key="tagItem.id"
          class="tag-row"
        >
          <label
            class="visually-hidden"
            :for="`tag-key-${tagItem.id}`"
          >Tag name</label>
          <input
            :id="`tag-key-${tagItem.id}`"
            :value="tagItem.key"
            type="text"
            placeholder="Name"
            :aria-invalid="tagError ? 'true' : 'false'"
            @input="updateTagKey(tagItem.id, $event)"
          >
          <label
            class="visually-hidden"
            :for="`tag-value-${tagItem.id}`"
          >Tag value</label>
          <input
            :id="`tag-value-${tagItem.id}`"
            :value="tagItem.value"
            type="text"
            placeholder="Value"
            :aria-invalid="tagError ? 'true' : 'false'"
            @input="updateTagValue(tagItem.id, $event)"
          >
          <button
            class="icon-only-button"
            type="button"
            title="Remove tag"
            aria-label="Remove tag"
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
</template>

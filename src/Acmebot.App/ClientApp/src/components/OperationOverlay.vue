<script setup lang="ts">
import { LoaderCircle } from 'lucide-vue-next';

defineProps<{
  active: boolean;
  title: string;
  message?: string;
}>();
</script>

<template>
  <Teleport to="body">
    <div
      v-if="active"
      class="operation-overlay"
      role="status"
      aria-live="polite"
    >
      <div class="operation-overlay__panel">
        <LoaderCircle
          class="operation-overlay__spinner"
          :size="28"
          aria-hidden="true"
        />
        <div>
          <div class="operation-overlay__title">
            {{ title }}
          </div>
          <div
            v-if="message"
            class="operation-overlay__message"
          >
            {{ message }}
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

import { setProjectAnnotations } from 'storybook/preview-api';
import { getProjectAnnotations } from 'virtual:/@storybook/builder-vite/project-annotations.js';

setProjectAnnotations(getProjectAnnotations());

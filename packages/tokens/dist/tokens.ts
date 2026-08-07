export const tokens = {
  "component": {
    "Button": {
      "background": "#ff5a1f",
      "foreground": "#171914",
      "disabledBackground": "#8b9189",
      "disabledForeground": "#fffdf6",
      "minBlockSize": "64px",
      "radius": "12px"
    },
    "NumberInput": {
      "surface": "#fffdf6",
      "border": "#c8c8be",
      "focus": "#ff5a1f",
      "minBlockSize": "64px",
      "radius": "12px"
    },
    "Dialog": {
      "surface": "#fffdf6",
      "border": "#c8c8be",
      "radius": "24px"
    },
    "ShipmentTaskCard": {
      "surface": "#fffdf6",
      "selected": "#ffc7b3",
      "border": "#c8c8be",
      "radius": "24px"
    },
    "PrinterPicker": {
      "surface": "#fffdf6",
      "selected": "#ffc7b3",
      "border": "#c8c8be",
      "focus": "#ff5a1f",
      "radius": "12px"
    }
  },
  "reference": {
    "color": {
      "registerInk": "#171914",
      "registerInkSoft": "#292c26",
      "labelPaper": "#f2f0e8",
      "labelPaperStrong": "#fffdf6",
      "warehouseSteel": "#8b9189",
      "registrationLine": "#c8c8be",
      "handoffOrange": "#ff5a1f",
      "handoffOrangeSoft": "#ffc7b3",
      "statusOk": "#267a53",
      "statusWarning": "#d08a00",
      "statusDanger": "#c93d33"
    },
    "space": {
      "2": "16px",
      "3": "24px",
      "4": "32px",
      "6": "48px",
      "unit": "8px"
    },
    "size": {
      "touch": {
        "minimum": "64px",
        "comfortable": "72px",
        "standard": "44px"
      }
    },
    "radius": {
      "label": "4px",
      "control": "12px",
      "surface": "24px"
    },
    "motion": {
      "fast": "120ms",
      "confirm": "180ms"
    },
    "font": {
      "sans": "Onest",
      "mono": "IBM Plex Mono"
    }
  },
  "semantic": {
    "color": {
      "action": {
        "primary": "#ff5a1f",
        "primarySoft": "#ffc7b3"
      },
      "selected": {
        "background": "#ffc7b3"
      },
      "focus": {
        "ring": "#ff5a1f",
        "offset": "#fffdf6"
      },
      "progress": {
        "track": "#f2f0e8",
        "fill": "#171914"
      },
      "canvas": {
        "default": "#f2f0e8"
      },
      "surface": {
        "default": "#fffdf6",
        "muted": "#f2f0e8"
      },
      "text": {
        "primary": "#171914",
        "secondary": "#292c26",
        "inverse": "#fffdf6"
      },
      "border": {
        "default": "#c8c8be"
      },
      "success": {
        "default": "#267a53"
      },
      "warning": {
        "default": "#d08a00"
      },
      "danger": {
        "default": "#c93d33"
      },
      "disabled": {
        "background": "#8b9189",
        "foreground": "#fffdf6",
        "border": "#c8c8be"
      }
    },
    "size": {
      "control": {
        "touch": "64px",
        "standard": "44px"
      }
    },
    "space": {
      "unit": "8px",
      "compact": "16px",
      "comfortable": "24px"
    },
    "radius": {
      "label": "4px",
      "control": "12px",
      "surface": "24px"
    },
    "motion": {
      "fast": "120ms",
      "confirm": "180ms"
    },
    "font": {
      "body": "Onest",
      "label": "IBM Plex Mono"
    }
  }
} as const;

export type PuntiroTokens = typeof tokens;

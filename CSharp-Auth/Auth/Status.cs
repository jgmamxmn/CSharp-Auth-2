

/*
 * Based on PHP-Auth (https://github.com/delight-im/PHP-Auth)
 * Copyright (c) Delight.im (https://www.delight.im/)
 * Licensed under the MIT License (https://opensource.org/licenses/MIT)
 */

namespace CSharpAuth.Auth {

	public enum Status {

	Undefined = -1,

	NORMAL = 0,
	ARCHIVED = 1,
	BANNED = 2,
	LOCKED = 3,
	PENDING_REVIEW = 4,
	SUSPENDED = 5

	}
}
